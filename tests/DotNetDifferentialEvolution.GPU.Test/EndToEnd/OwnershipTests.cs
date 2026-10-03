using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check 7b of the GPU package's ACCEPTANCE.md, ownership: an accelerator the caller created and
/// passed through <c>OnAccelerator</c> still allocates a buffer after the optimizer's
/// <c>Dispose</c>. The buffer is also filled by a kernel and read back: ILGPU's CPU accelerator
/// keeps allocating after its own <c>Dispose</c> (measured 2026-10-03) and fails only when a
/// kernel is launched, so allocation alone could not tell a disposed CPU accelerator from a live
/// one. On CUDA, allocation itself fails on a disposed accelerator. CPU accelerator in CI; CUDA
/// and OpenCL under <c>Gpu</c>.
/// </summary>
public class OwnershipTests
{
    private const int BufferLength = 16;

    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>A caller-owned CPU accelerator is still usable after the optimizer is disposed.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ACallerOwnedCpuAcceleratorOutlivesTheOptimizer()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);

        await AssertTheAcceleratorOutlivesTheOptimizer(accelerator).ConfigureAwait(true);
    }

    /// <summary>A caller-owned GPU accelerator is still usable after the optimizer is disposed.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public async Task ACallerOwnedGpuAcceleratorOutlivesTheOptimizer(GpuDevice device)
    {
        using var context = Context.Create(builder => _ = device == GpuDevice.Cuda ? builder.Cuda() : builder.OpenCL());
        using Accelerator accelerator = device == GpuDevice.Cuda ? context.CreateCudaAccelerator(0) : context.CreateCLAccelerator(0);

        await AssertTheAcceleratorOutlivesTheOptimizer(accelerator).ConfigureAwait(true);
    }

    /// <summary>Writes each index into its slot: a kernel launched on the accelerator after the optimizer is gone.</summary>
    /// <param name="index">The slot.</param>
    /// <param name="values">The buffer.</param>
    internal static void FillWithIndex(Index1D index, ArrayView<double> values) => values[index] = index;

    private static async Task AssertTheAcceleratorOutlivesTheOptimizer(Accelerator accelerator)
    {
        using (var optimizer = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
                   .WithBounds(Lower, Upper)
                   .WithPopulationSize(16)
                   .WithDefaultMutationStrategy(0.5, 0.9)
                   .WithGenerationLimit(5)
                   .OnAccelerator(accelerator)
                   .WithSeed(1)
                   .Build())
        {
            _ = await optimizer.RunAsync().ConfigureAwait(true);
        }

        using var buffer = accelerator.Allocate1D<double>(BufferLength);
        var fill = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>>(FillWithIndex);
        fill(BufferLength, buffer.View);
        accelerator.Synchronize();

        Assert.Equal(Enumerable.Range(0, BufferLength).Select(k => (double)k), buffer.GetAsArray1D());
    }
}
