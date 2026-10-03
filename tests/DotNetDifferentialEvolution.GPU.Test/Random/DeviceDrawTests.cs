namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// ACCEPTANCE.md, checks 3a and 4b under <b>Gpu</b>: inside a kernel on CUDA and on OpenCL,
/// Philox4x32-10 gives Random123's known answers (<see cref="PhiloxKatVectors"/>), and the first 10⁴
/// draws of a fixed (seed, individual, generation) are the host's words bit for bit. Local only:
/// CI excludes <c>Category=Gpu</c>.
/// </summary>
[Trait("Category", "Gpu")]
public class DeviceDrawTests
{
    /// <summary>Check 3a: the three known-answer vectors inside a kernel on the device.</summary>
    /// <param name="device">CUDA or OpenCL.</param>
    [Theory]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public void AKernelOnTheDeviceGivesTheKnownAnswers(GpuDevice device)
    {
        using var context = TestDevices.CreateContext(device);
        using var accelerator = TestDevices.CreateAccelerator(context, device);

        var blocks = PhiloxKatVectors.RunKernel(accelerator);

        Assert.Equal(PhiloxKatVectors.Expected, blocks);
    }

    /// <summary>Check 4b: the device draws the host's words.</summary>
    /// <param name="device">CUDA or OpenCL.</param>
    [Theory]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public void TheDeviceDrawsTheHostWords(GpuDevice device)
    {
        const int seed = 20261003;
        const int individual = 17;
        const int generation = 5;
        using var context = TestDevices.CreateContext(device);
        using var accelerator = TestDevices.CreateAccelerator(context, device);

        var words = DrawSequences.OnDevice(accelerator, seed, individual, generation);

        Assert.Equal(DrawSequences.OnHost(seed, individual, generation), words);
    }
}
