using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The two kernels of a run, compiled for one objective type, behind a non-generic face.</summary>
internal abstract class KernelLauncher
{
    /// <summary>Samples and evaluates the initial population into <see cref="PopulationViews.Current"/>.</summary>
    /// <param name="parameters">The launch parameters.</param>
    /// <param name="views">The device memory.</param>
    public abstract void Initialize(StepParameters parameters, PopulationViews views);

    /// <summary>Runs one generation from <see cref="PopulationViews.Current"/> into <see cref="PopulationViews.Next"/>.</summary>
    /// <param name="parameters">The launch parameters.</param>
    /// <param name="views">The device memory.</param>
    public abstract void Generation(StepParameters parameters, PopulationViews views);
}

/// <summary>The kernels compiled for <typeparamref name="TFunction"/> on one accelerator.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
/// <param name="accelerator">The accelerator; both kernels are compiled on it at construction, where ILGPU's compile errors surface.</param>
/// <param name="function">The objective, passed to every launch.</param>
internal sealed class KernelLauncher<TFunction>(Accelerator accelerator, TFunction function) : KernelLauncher
    where TFunction : struct, IGpuFitnessFunction
{
    private readonly TFunction _function = function;

    private readonly Action<Index1D, TFunction, StepParameters, PopulationViews> _initialize =
        accelerator.LoadAutoGroupedStreamKernel<Index1D, TFunction, StepParameters, PopulationViews>(GpuKernels.Initialize);

    private readonly Action<Index1D, TFunction, StepParameters, PopulationViews> _generation =
        accelerator.LoadAutoGroupedStreamKernel<Index1D, TFunction, StepParameters, PopulationViews>(GpuKernels.Generation);

    /// <inheritdoc />
    public override void Initialize(StepParameters parameters, PopulationViews views) =>
        _initialize(parameters.PopulationSize, _function, parameters, views);

    /// <inheritdoc />
    public override void Generation(StepParameters parameters, PopulationViews views) =>
        _generation(parameters.PopulationSize, _function, parameters, views);
}
