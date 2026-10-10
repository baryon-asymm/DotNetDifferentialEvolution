using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Checks A1 and A2 of the Devices node's ACCEPTANCE.md on ILGPU's CPU accelerator. A1: two <c>KernelLoader.Load</c> calls
/// for one kernel method on one accelerator return two <see cref="Kernel"/> objects, and disposing the first leaves the
/// second undisposed and working (the whole-run half is <c>EndToEnd/SharedKernelTests</c>). A2: <c>KernelLoader.GroupSize</c>
/// gives the known answers of the check; the launch on the device is <c>PointwiseLatencyTests</c>, under <c>Gpu</c>.
/// </summary>
[Trait("Category", "Integration")]
public class KernelLoaderTests
{
    private const int Length = 64;

    /// <summary>A2: the group size spreads the extent over the multiprocessors, in whole warps, between one warp and the occupancy limit.</summary>
    /// <param name="extent">The largest launch extent.</param>
    /// <param name="warpSize">The warp size.</param>
    /// <param name="multiprocessors">The number of multiprocessors.</param>
    /// <param name="occupancyLimit">The occupancy limit.</param>
    /// <param name="expected">The group size.</param>
    [Theory]
    [InlineData(1, 32, 70, 640, 32)]
    [InlineData(1024, 32, 70, 640, 32)]
    [InlineData(16_384, 32, 70, 640, 256)]
    [InlineData(44_800, 32, 70, 640, 640)]
    [InlineData(1_000_000, 32, 70, 640, 640)]
    [InlineData(1024, 64, 12, 256, 128)]
    public void TheGroupSizeSpreadsTheExtentOverTheMultiprocessors(int extent, int warpSize, int multiprocessors, int occupancyLimit, int expected) =>
        Assert.Equal(expected, KernelLoader.GroupSize(extent, warpSize, multiprocessors, occupancyLimit));

    /// <summary>An extent of <see cref="int.MaxValue"/> does not overflow the rounding.</summary>
    [Fact]
    public void TheLargestExtentDoesNotOverflow() =>
        Assert.Equal(640, KernelLoader.GroupSize(int.MaxValue, 32, 70, 640));

    /// <summary>An argument below 1 is refused, naming it.</summary>
    /// <param name="extent">The extent.</param>
    /// <param name="warpSize">The warp size.</param>
    /// <param name="multiprocessors">The number of multiprocessors.</param>
    /// <param name="occupancyLimit">The occupancy limit.</param>
    /// <param name="parameter">The argument that is below 1.</param>
    [Theory]
    [InlineData(0, 32, 70, 640, "extent")]
    [InlineData(1, 0, 70, 640, "warpSize")]
    [InlineData(1, 32, 0, 640, "multiprocessors")]
    [InlineData(1, 32, 70, 0, "occupancyLimit")]
    public void AnArgumentBelowOneIsRefused(int extent, int warpSize, int multiprocessors, int occupancyLimit, string parameter)
    {
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => KernelLoader.GroupSize(extent, warpSize, multiprocessors, occupancyLimit));

        Assert.Equal(parameter, failure.ParamName);
    }

    /// <summary>A1: two loads of one method are two kernels; the first's disposal leaves the second undisposed and launchable.</summary>
    [Fact]
    public void TwoLoadsOfOneMethodAreTwoKernelsDisposedIndependently()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        var method = typeof(KernelLoaderTests).GetMethod(nameof(Square), BindingFlags.NonPublic | BindingFlags.Static)!;

        var first = KernelLoader.Load(accelerator, method, Length);
        using var second = KernelLoader.Load(accelerator, method, Length);
        Assert.NotSame(first, second);

        first.Dispose();

        Assert.True(first.IsDisposed);
        Assert.False(second.IsDisposed);
        using var buffer = accelerator.Allocate1D<double>(Length);
        var launch = second.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<double>>>();
        launch(accelerator.DefaultStream, Length, buffer.View);
        accelerator.Synchronize();
        Assert.Equal(Enumerable.Range(0, Length).Select(k => (double)(k * k)), buffer.GetAsArray1D());
    }

    /// <summary>Writes the square of each index into its slot: the kernel the loads are of.</summary>
    /// <param name="index">The slot.</param>
    /// <param name="values">The buffer.</param>
    internal static void Square(Index1D index, ArrayView<double> values)
    {
        int i = index;
        values[i] = (double)i * i;
    }
}
