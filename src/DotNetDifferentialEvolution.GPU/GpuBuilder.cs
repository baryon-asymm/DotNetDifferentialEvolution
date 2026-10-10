using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// Every stage of the builder in one object. Each stage validates its own arguments, so an error
/// is reported where the wrong value is passed (API.md, errors); what depends on more than one stage
/// is refused by <see cref="Build"/>, as the CPU builder refuses it.
/// </summary>
/// <typeparam name="TFunction">The objective, a single-kernel or a pointwise one.</typeparam>
/// <param name="function">The objective.</param>
/// <param name="pointCount"><c>P</c> of a pointwise objective, or <see langword="null"/> for a single-kernel one.</param>
/// <param name="launcherFor">Compiles the kernels for the objective: its accelerator, the objective, the parameter rule and N.</param>
internal sealed class GpuBuilder<TFunction>(
    TFunction function,
    int? pointCount,
    Func<Accelerator, TFunction, ParameterRule, int, KernelLauncher> launcherFor)
    : IGpuBoundsRequired<TFunction>,
      IGpuPopulationSizeRequired<TFunction>,
      IGpuMutationStrategyRequired<TFunction>,
      IGpuTerminationConditionRequired<TFunction>,
      IGpuDeviceRequired<TFunction>,
      IGpuDifferentialEvolutionBuilder<TFunction>
    where TFunction : struct
{
    /// <summary>The largest N, or N·P, a kernel can index: a launch group is at most 1 024 threads, so the last group's thread index stays below <see cref="int.MaxValue"/>.</summary>
    private const int MaxThreadIndex = int.MaxValue - 1023;

    /// <summary>The largest N for which JADE, SHADE and L-SHADE rank the population: the ranking rounds N up to a power of two.</summary>
    private const int MaxRankedPopulationSize = 1 << 30;

    private readonly TFunction _function = function;
    private double[] _lowerBound = [];
    private double[] _upperBound = [];
    private int _populationSize;
    private StrategySettings? _strategy;
    private int? _maxGenerations;
    private long? _maxEvaluations;
    private (double Threshold, int MaxStreak)? _stagnation;
    private GpuDevice _device;
    private Accelerator? _accelerator;
    private int? _seed;
    private IGpuPopulationUpdatedHandler? _handler;
    private int _everyNGenerations = 1;
    private int _stopReadInterval = RunSettings.DefaultStopReadInterval;
    private Func<Backend, bool>? _isPresent;
    private Func<Accelerator, IDisposable>? _plantedRelease;

    /// <inheritdoc />
    public IGpuPopulationSizeRequired<TFunction> WithBounds(ReadOnlyMemory<double> lowerBound, ReadOnlyMemory<double> upperBound)
    {
        if (lowerBound.Length != upperBound.Length)
        {
            throw new ArgumentException(
                $"The bounds differ in length: {lowerBound.Length} lower, {upperBound.Length} upper.", nameof(upperBound));
        }

        if (lowerBound.IsEmpty)
        {
            throw new ArgumentException("The bounds are empty; a genome needs at least one gene.", nameof(lowerBound));
        }

        var lower = lowerBound.Span;
        var upper = upperBound.Span;
        for (var j = 0; j < lower.Length; j++)
        {
            if (!double.IsFinite(lower[j]) || !double.IsFinite(upper[j]))
            {
                throw new ArgumentException($"The bounds of gene {j} are not finite: [{lower[j]}, {upper[j]}].", nameof(lowerBound));
            }

            if (lower[j] > upper[j])
            {
                throw new ArgumentException($"The lower bound of gene {j}, {lower[j]}, exceeds its upper bound, {upper[j]}.", nameof(lowerBound));
            }
        }

        _lowerBound = lower.ToArray();
        _upperBound = upper.ToArray();
        return this;
    }

    /// <inheritdoc />
    public IGpuMutationStrategyRequired<TFunction> WithPopulationSize(int populationSize)
    {
        // The scheme's own minimum is known only at the next stage; Build refuses a population below it, as the CPU
        // builder does.
        ArgumentOutOfRangeException.ThrowIfLessThan(populationSize, 1);
        if (IndexRangeViolation(populationSize, _lowerBound.Length, pointCount) is { } violation)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize), populationSize, violation);
        }

        _populationSize = populationSize;
        return this;
    }

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability) =>
        UseFixed(nameof(WithDefaultMutationStrategy), SchemeKind.RandOne, mutationForce, crossoverProbability);

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithBestMutationStrategy(double mutationForce, double crossoverProbability) =>
        UseFixed(nameof(WithBestMutationStrategy), SchemeKind.Best, mutationForce, crossoverProbability);

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithCurrentToBestMutationStrategy(double mutationForce, double crossoverProbability) =>
        UseFixed(nameof(WithCurrentToBestMutationStrategy), SchemeKind.CurrentToBest, mutationForce, crossoverProbability);

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithRandTwoMutationStrategy(double mutationForce, double crossoverProbability) =>
        UseFixed(nameof(WithRandTwoMutationStrategy), SchemeKind.RandTwo, mutationForce, crossoverProbability);

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithBestTwoMutationStrategy(double mutationForce, double crossoverProbability) =>
        UseFixed(nameof(WithBestTwoMutationStrategy), SchemeKind.BestTwo, mutationForce, crossoverProbability);

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithJde(double initialMutationForce = 0.5, double initialCrossoverProbability = 0.9)
    {
        RequireMutationForce(initialMutationForce, nameof(initialMutationForce));
        RequireCrossoverProbability(initialCrossoverProbability, nameof(initialCrossoverProbability));
        _strategy = new StrategySettings(
            nameof(WithJde), SchemeKind.RandOne, ParameterRule.Jde, initialMutationForce, initialCrossoverProbability, false, 0.0, 0.0, 0, 0.0, null);
        return this;
    }

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithJade(double pBestRate = 0.1, double archiveSizeRate = 1.0, double adaptationRate = 0.1)
    {
        RequirePBestRate(pBestRate);
        RequireArchiveSizeRate(archiveSizeRate);
        if (adaptationRate is not (>= 0.0 and <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(adaptationRate), adaptationRate, "The adaptation rate c must be in [0, 1].");
        }

        _strategy = new StrategySettings(
            nameof(WithJade), SchemeKind.CurrentToPBest, ParameterRule.Jade, double.NaN, double.NaN, false, pBestRate, archiveSizeRate, 0, adaptationRate, null);
        return this;
    }

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithShade(double pBestRate = 0.2, double archiveSizeRate = 1.0, int memorySize = 100)
    {
        RequirePBestRate(pBestRate);
        RequireArchiveSizeRate(archiveSizeRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(memorySize, 1);
        _strategy = new StrategySettings(
            nameof(WithShade), SchemeKind.CurrentToPBest, ParameterRule.Shade, double.NaN, double.NaN, true, pBestRate, archiveSizeRate, memorySize, 0.0, null);
        return this;
    }

    /// <inheritdoc />
    public IGpuTerminationConditionRequired<TFunction> WithLShade(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEvaluationNumber, 1L);
        RequirePBestRate(pBestRate);
        RequireArchiveSizeRate(archiveSizeRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(memorySize, 1);
        _strategy = new StrategySettings(
            nameof(WithLShade),
            SchemeKind.CurrentToPBest,
            ParameterRule.Shade,
            double.NaN,
            double.NaN,
            true,
            pBestRate,
            archiveSizeRate,
            memorySize,
            0.0,
            maxEvaluationNumber);
        return this;
    }

    /// <inheritdoc />
    public IGpuDeviceRequired<TFunction> WithGenerationLimit(int maxGenerations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGenerations, 1);
        _maxGenerations = maxGenerations;
        return this;
    }

    /// <inheritdoc />
    public IGpuDeviceRequired<TFunction> WithEvaluationLimit(long maxEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEvaluations, 1L);
        _maxEvaluations = maxEvaluations;
        return this;
    }

    /// <inheritdoc />
    public IGpuDeviceRequired<TFunction> WithStagnationLimit(int maxStagnationStreak, double stagnationThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxStagnationStreak, 1);
        if (!double.IsFinite(stagnationThreshold) || stagnationThreshold < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stagnationThreshold), stagnationThreshold, "The stagnation threshold must be finite and not negative.");
        }

        _stagnation = (stagnationThreshold, maxStagnationStreak);
        return this;
    }

    /// <inheritdoc />
    public IGpuDifferentialEvolutionBuilder<TFunction> OnDevice(GpuDevice device)
    {
        if (!Enum.IsDefined(device))
        {
            throw new ArgumentOutOfRangeException(nameof(device), device, "Not a defined GpuDevice.");
        }

        _device = device;
        return this;
    }

    /// <inheritdoc />
    public IGpuDifferentialEvolutionBuilder<TFunction> OnAccelerator(Accelerator accelerator)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        if (accelerator.AcceleratorType is not (AcceleratorType.Cuda or AcceleratorType.OpenCL or AcceleratorType.CPU))
        {
            throw new ArgumentException(
                $"The accelerator is of type {accelerator.AcceleratorType}; only CUDA, OpenCL and CPU accelerators are supported.",
                nameof(accelerator));
        }

        _accelerator = accelerator;
        return this;
    }

    /// <inheritdoc />
    public IGpuDifferentialEvolutionBuilder<TFunction> WithSeed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <inheritdoc />
    public IGpuDifferentialEvolutionBuilder<TFunction> WithPopulationUpdateHandler(IGpuPopulationUpdatedHandler handler, int everyNGenerations = 1)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentOutOfRangeException.ThrowIfLessThan(everyNGenerations, 1);
        _handler = handler;
        _everyNGenerations = everyNGenerations;
        return this;
    }

    /// <inheritdoc />
    public GpuDifferentialEvolution Build()
    {
        // The staged interfaces reach Build only through a scheme stage.
        ValidateConfiguration();
        var strategy = _strategy!;

        // An unseeded run still has one seed, drawn here; the cryptographic generator only
        // because the analyzers refuse System.Random (CA5394), not because the seed is a secret.
        var settings = new RunSettings(
            _lowerBound,
            _upperBound,
            _populationSize,
            strategy,
            _maxGenerations,
            _maxEvaluations,
            _stagnation,
            _seed ?? RandomNumberGenerator.GetInt32(int.MaxValue),
            _handler,
            _everyNGenerations)
        {
            StopReadInterval = _stopReadInterval,
        };
        var function = _function;
        var populationSize = _populationSize;
        KernelLauncher Compile(Accelerator accelerator) => launcherFor(accelerator, function, strategy.Rule, populationSize);

        // The lease goes straight into the constructor, which owns it from then on. ILGPU binds every accelerator it creates, and
        // the binding of a thread can be given back only by disposing the accelerator, so the device is opened on a thread of
        // its own: the caller's thread keeps the binding it had (ACCEPTANCE.md, A11).
        var callersAccelerator = _accelerator;
        var backend = BackendOf(_device);
        var isPresent = _isPresent;
        var plantedRelease = _plantedRelease;
        return OnAThreadOfItsOwn(
            () => callersAccelerator is not null
                ? new GpuDifferentialEvolution(AcceleratorLease.Borrowed(callersAccelerator), settings, Compile, plantedRelease)
                : new GpuDifferentialEvolution(DeviceSelector.Open(backend, LibDeviceLocator.Locate, isPresent), settings, Compile, plantedRelease));
    }

    /// <summary>
    /// Runs every check <see cref="Build"/> makes before it opens a device, and nothing else: for the tests of check A9, whose
    /// accepted edges are populations too large to allocate (ACCEPTANCE.md, A9).
    /// </summary>
    /// <exception cref="InvalidOperationException">The configuration is one <see cref="Build"/> refuses.</exception>
    internal void ValidateConfiguration() => Validate(_strategy!);

    /// <summary>
    /// Reads the stop word every <paramref name="interval"/> generations instead of every
    /// <see cref="RunSettings.DefaultStopReadInterval"/>: for the tests that a stagnation limit ends a run at the same
    /// generation whatever the interval (ACCEPTANCE.md, S12, S17).
    /// </summary>
    /// <param name="interval">The interval; at least 1.</param>
    /// <returns>This builder.</returns>
    internal GpuBuilder<TFunction> WithStopReadInterval(int interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, 1);
        _stopReadInterval = interval;
        return this;
    }

    /// <summary>
    /// Decides by <paramref name="isPresent"/> whether a backend has a device, instead of asking ILGPU: for the tests of
    /// check D1 that must run on a machine with CUDA and OpenCL without opening either (ACCEPTANCE.md, A12). A backend it
    /// denies is skipped, or refused when explicit, before any context for it exists. Ignored when the caller's accelerator
    /// is used.
    /// </summary>
    /// <param name="isPresent">Whether the backend has a device.</param>
    /// <returns>This builder.</returns>
    internal GpuBuilder<TFunction> WithDevicePresence(Func<Backend, bool> isPresent)
    {
        ArgumentNullException.ThrowIfNull(isPresent);
        _isPresent = isPresent;
        return this;
    }

    /// <summary>
    /// Makes the optimizer, as <see cref="Build"/> does, add to its own releases the one <paramref name="plant"/> creates on its
    /// accelerator, first of them all: for the tests of check A6, whose subject is a release that throws (ACCEPTANCE.md, A6).
    /// </summary>
    /// <param name="plant">Creates the release on the accelerator; its disposal is what throws.</param>
    /// <returns>This builder.</returns>
    internal GpuBuilder<TFunction> WithPlantedRelease(Func<Accelerator, IDisposable> plant)
    {
        ArgumentNullException.ThrowIfNull(plant);
        _plantedRelease = plant;
        return this;
    }

    /// <summary>Runs <paramref name="build"/> on a new thread and waits for it; its exception is rethrown unchanged, with its stack.</summary>
    private static GpuDifferentialEvolution OnAThreadOfItsOwn(Func<GpuDifferentialEvolution> build)
    {
        GpuDifferentialEvolution? built = null;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                built = build();
            }
            catch (Exception exception) when (Capture(exception, out failure))
            {
                // Captured by the filter, to be rethrown on the caller's thread.
            }
        })
        {
            Name = nameof(GpuDifferentialEvolution) + " build",
            IsBackground = true,
        };
        thread.Start();
        thread.Join();
        failure?.Throw();
        return built!;
    }

    private static bool Capture(Exception exception, out ExceptionDispatchInfo captured)
    {
        captured = ExceptionDispatchInfo.Capture(exception);
        return true;
    }

    private static void RequireMutationForce(double mutationForce, string name)
    {
        if (!double.IsFinite(mutationForce) || mutationForce <= 0.0)
        {
            throw new ArgumentOutOfRangeException(name, mutationForce, "F must be finite and greater than 0.");
        }
    }

    private static void RequireCrossoverProbability(double crossoverProbability, string name)
    {
        if (crossoverProbability is not (>= 0.0 and <= 1.0))
        {
            throw new ArgumentOutOfRangeException(name, crossoverProbability, "CR must be in [0, 1].");
        }
    }

    private static void RequirePBestRate(double pBestRate)
    {
        if (pBestRate is not (> 0.0 and <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(pBestRate), pBestRate, "The p-best rate must be in (0, 1].");
        }
    }

    private static void RequireArchiveSizeRate(double archiveSizeRate)
    {
        if (!double.IsFinite(archiveSizeRate) || archiveSizeRate < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(archiveSizeRate), archiveSizeRate, "The archive size rate must be finite and not negative.");
        }
    }

    /// <summary>
    /// Why a population of <paramref name="populationSize"/> individuals of <paramref name="geneCount"/> genes and
    /// <paramref name="points"/> points cannot be indexed by a kernel, or <see langword="null"/> when it can (ACCEPTANCE.md, A9).
    /// N and N·P stay <see cref="MaxThreadIndex"/> or below, so that the last launch group's thread index cannot wrap; N·D stays
    /// within <see cref="int.MaxValue"/>.
    /// </summary>
    private static string? IndexRangeViolation(int populationSize, int geneCount, int? points)
    {
        var genes = (long)populationSize * geneCount;
        var results = (long)populationSize * (points ?? 0);
        return populationSize > MaxThreadIndex
            ? $"N = {populationSize} exceeds {MaxThreadIndex}, the largest population whose last launch group a kernel can index."
            : genes > int.MaxValue
                ? $"N·D = {genes} exceeds {int.MaxValue}, the largest population a kernel can index."
                : results > MaxThreadIndex
                    ? $"N·P = {results} exceeds {MaxThreadIndex}, the most point results a kernel can index."
                    : null;
    }

    private static Backend? BackendOf(GpuDevice device) => device switch
    {
        GpuDevice.Cuda => Backend.Cuda,
        GpuDevice.OpenCL => Backend.OpenCL,
        GpuDevice.Cpu => Backend.Cpu,
        GpuDevice.Auto => null,
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Not a defined GpuDevice."),
    };

    private GpuBuilder<TFunction> UseFixed(string name, SchemeKind scheme, double mutationForce, double crossoverProbability)
    {
        RequireMutationForce(mutationForce, nameof(mutationForce));
        RequireCrossoverProbability(crossoverProbability, nameof(crossoverProbability));
        _strategy = StrategySettings.Fixed(name, scheme, mutationForce, crossoverProbability);
        return this;
    }

    /// <summary>What the CPU builder refuses in its <c>Build</c>, refused here as there (API.md, errors).</summary>
    private void Validate(StrategySettings strategy)
    {
        // The stages checked these as each was set; WithBounds may have been called again since (a retained stage).
        if (IndexRangeViolation(_populationSize, _lowerBound.Length, pointCount) is { } violation)
        {
            throw new InvalidOperationException(violation);
        }

        if (strategy.Rule is ParameterRule.Jade or ParameterRule.Shade && _populationSize > MaxRankedPopulationSize)
        {
            throw new InvalidOperationException(
                $"Population size {_populationSize} is too large for {strategy.Name}, which ranks the individuals by fitness " +
                $"in a network over N rounded up to a power of two: at most {MaxRankedPopulationSize} (2^30) individuals.");
        }

        if (_populationSize < strategy.MinimumPopulationSize)
        {
            throw new InvalidOperationException(
                $"Population size {_populationSize} is too small for {strategy.Name}, which needs at least " +
                $"{strategy.MinimumPopulationSize} individuals to draw the distinct vectors it requires.");
        }

        if (strategy.LShadeBudget is { } budget && _maxEvaluations is { } maxEvaluations && maxEvaluations != budget)
        {
            throw new InvalidOperationException(
                $"L-SHADE was configured with an evaluation budget of {budget}, but the evaluation limit is " +
                $"{maxEvaluations}. They must match so the linear population-size reduction reaches its minimum exactly " +
                "as the run terminates.");
        }

        // In double: a large rate would overflow the capacity's int before the product is formed.
        var archiveGenes = Math.Round(strategy.ArchiveSizeRate * _populationSize, MidpointRounding.AwayFromZero) * _lowerBound.Length;
        if (archiveGenes > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"The archive of {strategy.Name} would hold {archiveGenes} genes, more than {int.MaxValue}, the largest buffer a kernel can index.");
        }
    }
}
