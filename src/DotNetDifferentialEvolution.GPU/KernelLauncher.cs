using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The kernels of a run, compiled for one objective type, behind a non-generic face.</summary>
internal abstract class KernelLauncher : IDisposable
{
    /// <summary>Samples and evaluates the initial population into <see cref="PopulationViews.Current"/>.</summary>
    /// <param name="parameters">The launch parameters.</param>
    /// <param name="views">The device memory.</param>
    public abstract void Initialize(StepParameters parameters, PopulationViews views);

    /// <summary>Runs one generation from <see cref="PopulationViews.Current"/> into <see cref="PopulationViews.Next"/>.</summary>
    /// <param name="parameters">The launch parameters.</param>
    /// <param name="views">The device memory.</param>
    /// <param name="strategy">The device state of the scheme and the parameter rule.</param>
    public abstract void Generation(StepParameters parameters, PopulationViews views, StrategyViews strategy);

    /// <summary>Releases the kernels and any buffers the launcher owns.</summary>
    public abstract void Dispose();

    /// <summary>The type of the parameter rule a generation or build kernel is compiled for.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The rule's struct.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rule"/> is not a defined rule.</exception>
    protected static Type RuleType(ParameterRule rule) => rule switch
    {
        ParameterRule.Fixed => typeof(FixedRule),
        ParameterRule.Jde => typeof(JdeRule),
        ParameterRule.Jade => typeof(JadeRule),
        ParameterRule.Shade => typeof(ShadeRule),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Not a defined parameter rule."),
    };
}

/// <summary>
/// The kernels compiled for <typeparamref name="TFunction"/> on one accelerator, each loaded through
/// <see cref="KernelLoader"/>, so that on CUDA the objective's math is completed by the libdevice post-link.
/// </summary>
/// <typeparam name="TFunction">The objective.</typeparam>
internal sealed class KernelLauncher<TFunction> : KernelLauncher
    where TFunction : struct, IGpuFitnessFunction
{
    private readonly AcceleratorStream _stream;
    private readonly TFunction _function;
    private readonly Kernel _initializeKernel;
    private readonly Kernel _generationKernel;
    private readonly Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews> _initialize;
    private readonly Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews, StrategyViews> _generation;

    /// <summary>Compiles and loads both kernels; ILGPU's compile errors and the post-link's surface here.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="function">The objective, passed to every launch.</param>
    /// <param name="rule">The parameter rule the generation kernel is compiled for.</param>
    public KernelLauncher(Accelerator accelerator, TFunction function, ParameterRule rule = ParameterRule.Fixed)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _stream = accelerator.DefaultStream;
        _function = function;
        _initializeKernel = KernelLoader.Load(accelerator, Entry(nameof(GpuKernels.Initialize), typeof(TFunction)));
        try
        {
            _generationKernel = KernelLoader.Load(accelerator, Entry(nameof(GpuKernels.Generation), typeof(TFunction), RuleType(rule)));
            try
            {
                _initialize = _initializeKernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews>>();
                _generation = _generationKernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews, StrategyViews>>();
            }
            catch
            {
                _generationKernel.Dispose();
                throw;
            }
        }
        catch
        {
            _initializeKernel.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override void Initialize(StepParameters parameters, PopulationViews views) =>
        _initialize(_stream, parameters.PopulationSize, _function, parameters, views);

    /// <inheritdoc />
    public override void Generation(StepParameters parameters, PopulationViews views, StrategyViews strategy) =>
        _generation(_stream, parameters.PopulationSize, _function, parameters, views, strategy);

    /// <inheritdoc />
    public override void Dispose()
    {
        _initializeKernel.Dispose();
        _generationKernel.Dispose();
    }

    private static MethodInfo Entry(string name, params Type[] typeArguments) =>
        typeof(GpuKernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeArguments);
}
