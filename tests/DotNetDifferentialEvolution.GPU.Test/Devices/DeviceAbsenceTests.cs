using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Checks D1 and L6 and B1's row "an explicit device that is not present", on every machine and without opening a
/// device: the presence of CUDA and OpenCL is injected (<c>GpuBuilder.WithDevicePresence</c>,
/// <c>DeviceSelector.Open</c>'s third argument), the absent backends are refused before a context for them exists, and what
/// opens is the CPU accelerator. On a machine that has the devices they are neither asked nor created (check A12); the same
/// checks on the machine's own devices are <see cref="DeviceSelectionTests"/> and <c>CudaLibDeviceTests</c>, under <c>Gpu</c>.
/// </summary>
/// <param name="output">Receives the messages and the device each case got.</param>
public class DeviceAbsenceTests(ITestOutputHelper output)
{
    /// <summary>D1: with no CUDA and no OpenCL device present, <c>Auto</c> gives the CPU accelerator with the reason each was skipped.</summary>
    [Fact]
    public void AutoFallsBackToTheCpuWithTheReasonsWhenNoGpuIsPresent()
    {
        using var optimizer = Absent(DeviceSelectionTests.Stage().OnDevice(GpuDevice.Auto).WithSeed(1)).Build();
        var device = optimizer.Device;
        output.WriteLine(device.ToString());

        Assert.Equal(GpuDevice.Cpu, device.Kind);
        Assert.Equal("CUDA: no such device is present.; OpenCL: no such device is present.", device.FallbackReason);
    }

    /// <summary>
    /// D1 and B1: with the device absent, an explicit <c>Cuda</c> or <c>OpenCL</c> makes <c>Build</c> throw an
    /// <see cref="InvalidOperationException"/> naming it and saying it was requested, never falling back.
    /// </summary>
    /// <param name="device">The device asked for.</param>
    /// <param name="name">The name the message must carry.</param>
    [Theory]
    [InlineData(GpuDevice.Cuda, "CUDA")]
    [InlineData(GpuDevice.OpenCL, "OpenCL")]
    public void AnExplicitDeviceThatIsNotPresentFailsBuildNamingIt(GpuDevice device, string name)
    {
        var stage = Absent(DeviceSelectionTests.Stage().OnDevice(device).WithSeed(1));

        var failure = Assert.Throws<InvalidOperationException>(stage.Build);
        output.WriteLine(failure.Message);

        Assert.Equal($"The {name} device was requested and cannot be used: no such device is present.", failure.Message);
    }

    /// <summary>
    /// The seam is asked, in Auto's order, for each backend until one opens, and a backend it denies is skipped: CUDA,
    /// OpenCL, then the CPU, which it affirms.
    /// </summary>
    [Fact]
    public void TheInjectedPresenceIsAskedInAutosOrderUntilABackendOpens()
    {
        var asked = new List<Backend>();

        using var lease = DeviceSelector.Open(
            null,
            NoToolkit,
            backend =>
            {
                asked.Add(backend);
                return backend == Backend.Cpu;
            });

        Assert.Equal([Backend.Cuda, Backend.OpenCL, Backend.Cpu], asked);
        Assert.Equal(Backend.Cpu, lease.Backend);
    }

    /// <summary>
    /// L6: with a CUDA device present and the locator finding nothing, an explicit CUDA request throws naming CUDA, libnvvm
    /// and libdevice; Auto skips CUDA with that reason. The device is injected as present, on any machine, and the refusal
    /// comes before a CUDA context exists: nothing here opens a device (check A12).
    /// </summary>
    [Fact]
    public void WithoutAToolkitCudaIsRefusedWithTheReason()
    {
        static bool CudaAndTheCpu(Backend backend) => backend is Backend.Cuda or Backend.Cpu;

        var failure = Assert.Throws<InvalidOperationException>(() => DeviceSelector.Open(Backend.Cuda, NoToolkit, CudaAndTheCpu));
        output.WriteLine(failure.Message);
        Assert.Contains("CUDA", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"libnvvm ({LibDeviceLocator.LibraryFileName}) and libdevice ({LibDeviceLocator.BitcodeName})", failure.Message, StringComparison.Ordinal);

        using var auto = DeviceSelector.Open(null, NoToolkit, CudaAndTheCpu);
        output.WriteLine($"Auto: {auto.Backend}; {auto.FallbackReason}");
        Assert.Equal(Backend.Cpu, auto.Backend);
        Assert.Contains("CUDA: libnvvm", auto.FallbackReason, StringComparison.Ordinal);
    }

    /// <summary>L6, without a CUDA device: the device, not the toolkit, is the reason, whether or not a toolkit is found.</summary>
    [Fact]
    public void WithoutACudaDeviceTheDeviceIsTheReason()
    {
        static bool OnlyTheCpu(Backend backend) => backend == Backend.Cpu;

        var failure = Assert.Throws<InvalidOperationException>(() => DeviceSelector.Open(Backend.Cuda, NoToolkit, OnlyTheCpu));
        output.WriteLine(failure.Message);
        Assert.Contains("CUDA", failure.Message, StringComparison.Ordinal);
        Assert.Contains("no such device", failure.Message, StringComparison.Ordinal);

        using var auto = DeviceSelector.Open(null, NoToolkit, OnlyTheCpu);
        output.WriteLine($"Auto: {auto.Backend}; {auto.FallbackReason}");
        Assert.Equal(Backend.Cpu, auto.Backend);
        Assert.Contains("CUDA: no such device", auto.FallbackReason, StringComparison.Ordinal);
    }

    /// <summary>The builder told that only the CPU accelerator is present: no CUDA and no OpenCL device, whatever the machine has.</summary>
    private static GpuBuilder<SumOfSquares> Absent(IGpuDifferentialEvolutionBuilder<SumOfSquares> builder) =>
        ((GpuBuilder<SumOfSquares>)builder).WithDevicePresence(backend => backend == Backend.Cpu);

    private static LibDeviceLocation NoToolkit() => new(null, null, []);
}
