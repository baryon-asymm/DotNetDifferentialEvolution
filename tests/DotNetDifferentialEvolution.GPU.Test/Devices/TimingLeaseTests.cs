using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check A14 of the Devices node's ACCEPTANCE.md on ILGPU's CPU accelerator: a lease from
/// <c>DeviceSelector.OpenForTiming</c> measures device time with two profiling markers around a launched kernel and a
/// synchronisation, and a lease from <c>DeviceSelector.Open</c> keeps profiling off, so adding a marker throws ILGPU's
/// exception for profiling disabled. The device-time checks that use the lease are <c>Bookkeeping/BookkeepingTimingTests</c>,
/// under <c>Gpu</c>.
/// </summary>
[Trait("Category", "Integration")]
public class TimingLeaseTests
{
    private const int Length = 64;

    /// <summary>A14: the markers around a kernel launch and a synchronisation measure a non-negative time.</summary>
    [Fact]
    public void ALeaseForTimingMeasuresATimeBetweenTwoMarkers()
    {
        using var lease = DeviceSelector.OpenForTiming(Backend.Cpu);
        var accelerator = lease.Accelerator;
        var method = typeof(KernelLoaderTests).GetMethod(nameof(KernelLoaderTests.Square), BindingFlags.NonPublic | BindingFlags.Static)!;
        using var kernel = KernelLoader.Load(accelerator, method, Length);
        using var buffer = accelerator.Allocate1D<double>(Length);
        var launch = kernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ArrayView<double>>>();

        var start = accelerator.DefaultStream.AddProfilingMarker();
        launch(accelerator.DefaultStream, Length, buffer.View);
        accelerator.Synchronize();
        var end = accelerator.DefaultStream.AddProfilingMarker();
        end.Synchronize();

        Assert.Equal(Backend.Cpu, lease.Backend);
        Assert.True(end.MeasureFrom(start) >= TimeSpan.Zero, "the time between the markers is negative");
    }

    /// <summary>A14: a lease from <c>Open</c> has profiling off; a marker throws ILGPU's exception for profiling disabled.</summary>
    [Fact]
    public void ALeaseFromOpenKeepsProfilingOff()
    {
        using var lease = DeviceSelector.Open(Backend.Cpu);

        var stream = lease.Accelerator.DefaultStream;

        var failure = Assert.Throws<NotSupportedException>(stream.AddProfilingMarker);

        Assert.Contains("profiling", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}
