using System.Security.Cryptography;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// Every stage of the builder in one object. Each stage validates its own arguments, so an error
/// is reported where the wrong value is passed (API.md, errors); what depends on more than one stage
/// is refused by <see cref="Build"/>, as the CPU builder refuses it.
/// </summary>
/// <typeparam name="TFunction">The objective.</typeparam>
/// <param name="function">The objective.</param>
internal sealed class GpuBuilder<TFunction>(TFunction function)
    : IGpuBoundsRequired<TFunction>,
      IGpuPopulationSizeRequired<TFunction>,
      IGpuMutationStrategyRequired<TFunction>,
      IGpuTerminationConditionRequired<TFunction>,
      IGpuDeviceRequired<TFunction>,
      IGpuDifferentialEvolutionBuilder<TFunction>
    where TFunction : struct, IGpuFitnessFunction
{
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
        if ((long)populationSize * _lowerBound.Length > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(populationSize),
                populationSize,
                $"N·D = {(long)populationSize * _lowerBound.Length} exceeds {int.MaxValue}, the largest population a kernel can index.");
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
            nameof(WithJde), SchemeKind.RandOne, ParameterRule.Jde, initialMutationForce, initialCrossoverProbability, true, 0.0, 0.0, 0, 0.0, null);
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
        var strategy = _strategy!;
        Validate(strategy);

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
            _everyNGenerations);
        var function = _function;
        KernelLauncher Compile(Accelerator accelerator) => new KernelLauncher<TFunction>(accelerator, function, strategy.Rule);

        // The lease goes straight into the constructor, which owns it from then on.
        return _accelerator is { } callersAccelerator
            ? new GpuDifferentialEvolution(AcceleratorLease.Borrowed(callersAccelerator), settings, Compile)
            : new GpuDifferentialEvolution(DeviceSelector.Open(BackendOf(_device)), settings, Compile);
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
