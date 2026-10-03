using System.Security.Cryptography;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// Every stage of the builder in one object. Each stage validates its own arguments, so an error
/// is reported where the wrong value is passed (API.md, errors of v1).
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
    /// <summary>DE/rand/1 needs the individual and three distinct others.</summary>
    private const int MinimumPopulationSize = 4;

    private readonly TFunction _function = function;
    private double[] _lowerBound = [];
    private double[] _upperBound = [];
    private int _populationSize;
    private double _mutationForce;
    private double _crossoverProbability;
    private int? _maxGenerations;
    private long? _maxEvaluations;
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
        ArgumentOutOfRangeException.ThrowIfLessThan(populationSize, MinimumPopulationSize);
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
    public IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability)
    {
        if (!double.IsFinite(mutationForce) || mutationForce <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationForce), mutationForce, "F must be finite and greater than 0.");
        }

        if (crossoverProbability is not (>= 0.0 and <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(crossoverProbability), crossoverProbability, "CR must be in [0, 1].");
        }

        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
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
        // An unseeded run still has one seed, drawn here; the cryptographic generator only
        // because the analyzers refuse System.Random (CA5394), not because the seed is a secret.
        var settings = new RunSettings(
            _lowerBound,
            _upperBound,
            _populationSize,
            _mutationForce,
            _crossoverProbability,
            _maxGenerations,
            _maxEvaluations,
            _seed ?? RandomNumberGenerator.GetInt32(int.MaxValue),
            _handler,
            _everyNGenerations);
        var function = _function;
        KernelLauncher Compile(Accelerator accelerator) => new KernelLauncher<TFunction>(accelerator, function);

        // The lease goes straight into the constructor, which owns it from then on.
        return _accelerator is { } callersAccelerator
            ? new GpuDifferentialEvolution(AcceleratorLease.Borrowed(callersAccelerator), settings, Compile)
            : new GpuDifferentialEvolution(DeviceSelector.Open(BackendOf(_device)), settings, Compile);
    }

    private static Backend? BackendOf(GpuDevice device) => device switch
    {
        GpuDevice.Cuda => Backend.Cuda,
        GpuDevice.OpenCL => Backend.OpenCL,
        GpuDevice.Cpu => Backend.Cpu,
        GpuDevice.Auto => null,
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Not a defined GpuDevice."),
    };
}
