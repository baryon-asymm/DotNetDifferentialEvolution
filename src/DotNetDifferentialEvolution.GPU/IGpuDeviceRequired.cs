using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>The fifth stage of the builder: where the run executes.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuDeviceRequired<TFunction>
    where TFunction : struct, IGpuFitnessFunction
{
    /// <summary>
    /// Runs on <paramref name="device"/>, opened by <see cref="IGpuDifferentialEvolutionBuilder{TFunction}.Build"/>
    /// and disposed with the optimizer. An explicit device that cannot be opened makes <c>Build</c>
    /// throw; only <see cref="GpuDevice.Auto"/> falls back.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <returns>The last stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="device"/> is not a defined value.</exception>
    IGpuDifferentialEvolutionBuilder<TFunction> OnDevice(GpuDevice device);

    /// <summary>
    /// Runs on the caller's accelerator, which the optimizer never disposes. The objective's own
    /// <c>ArrayView</c>s must live on the same accelerator.
    /// </summary>
    /// <param name="accelerator">A CUDA, OpenCL or CPU accelerator.</param>
    /// <returns>The last stage.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accelerator"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The accelerator is of another type.</exception>
    IGpuDifferentialEvolutionBuilder<TFunction> OnAccelerator(Accelerator accelerator);
}
