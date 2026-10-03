using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The two kernels of a run, compiled for one objective type, behind a non-generic face.</summary>
internal abstract class KernelLauncher : IDisposable
{
    /// <summary>Samples and evaluates the initial population into <see cref="PopulationViews.Current"/>.</summary>
    /// <param name="parameters">The launch parameters.</param>
    /// <param name="views">The device memory.</param>
    public abstract void Initialize(StepParameters parameters, PopulationViews views);

    /// <summary>Runs one generation from <see cref="PopulationViews.Current"/> into <see cref="PopulationViews.Next"/>.</summary>
    /// <param name="parameters">The launch parameters.</param>
    /// <param name="views">The device memory.</param>
    public abstract void Generation(StepParameters parameters, PopulationViews views);

    /// <summary>Releases the two kernels.</summary>
    public abstract void Dispose();
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
    private readonly Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews> _generation;

    /// <summary>Compiles and loads both kernels; ILGPU's compile errors and the post-link's surface here.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="function">The objective, passed to every launch.</param>
    public KernelLauncher(Accelerator accelerator, TFunction function)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        _stream = accelerator.DefaultStream;
        _function = function;
        _initializeKernel = KernelLoader.Load(accelerator, Entry(nameof(GpuKernels.Initialize)));
        try
        {
            _generationKernel = KernelLoader.Load(accelerator, Entry(nameof(GpuKernels.Generation)));
            try
            {
                _initialize = _initializeKernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews>>();
                _generation = _generationKernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, TFunction, StepParameters, PopulationViews>>();
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
    public override void Generation(StepParameters parameters, PopulationViews views) =>
        _generation(_stream, parameters.PopulationSize, _function, parameters, views);

    /// <inheritdoc />
    public override void Dispose()
    {
        _initializeKernel.Dispose();
        _generationKernel.Dispose();
    }

    private static MethodInfo Entry(string name) =>
        typeof(GpuKernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(TFunction));
}
