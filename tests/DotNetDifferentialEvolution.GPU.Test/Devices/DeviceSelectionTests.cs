using DotNetDifferentialEvolution.GPU.Objectives;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check D1 of the GPU package's ACCEPTANCE.md and B1's row "an explicit device that is not present", on the
/// machine's own devices: every case carries <c>Gpu</c>. The cases ask the machine
/// (<see cref="DevicePresence"/>) and assert the branch that matches it, and name the two devices of the owner's
/// machine, RTX 5070 Ti through CUDA and <c>gfx1036</c> through OpenCL: they are machine-specific by the check's own
/// wording and fail elsewhere. The same checks with the devices injected as absent, which run everywhere and open no
/// device, are <see cref="DeviceAbsenceTests"/>.
/// </summary>
/// <param name="output">Receives the device each case got.</param>
public class DeviceSelectionTests(ITestOutputHelper output)
{
    private static readonly double[] Lower = [-1.0, -1.0];
    private static readonly double[] Upper = [1.0, 1.0];

    /// <summary>D1, under <c>Gpu</c>: <c>Auto</c> takes CUDA with no reason, or OpenCL with the reason CUDA was skipped, or the CPU with both reasons.</summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void AutoTakesTheBestDeviceThisMachineHas()
    {
        var hasCuda = DevicePresence.HasCuda;
        var hasOpenCL = DevicePresence.HasOpenCL;

        using var optimizer = Stage().OnDevice(GpuDevice.Auto).WithSeed(1).Build();
        var device = optimizer.Device;
        output.WriteLine($"CUDA present: {hasCuda}, OpenCL present: {hasOpenCL}; Auto gave {device}");

        if (!hasCuda && !hasOpenCL)
        {
            Assert.Equal(GpuDevice.Cpu, device.Kind);
            Assert.NotNull(device.FallbackReason);
        }
        else if (hasCuda)
        {
            Assert.Equal(GpuDevice.Cuda, device.Kind);
            Assert.Null(device.FallbackReason);
        }
        else
        {
            Assert.Equal(GpuDevice.OpenCL, device.Kind);
            Assert.Contains("CUDA", device.FallbackReason, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// B1, under <c>Gpu</c>: an explicit device is used with no fallback reason where this machine has it, and refused
    /// naming it where it does not.
    /// </summary>
    /// <param name="device">The device asked for.</param>
    /// <param name="name">The name the message must carry.</param>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda, "CUDA")]
    [InlineData(GpuDevice.OpenCL, "OpenCL")]
    public void AnExplicitDeviceIsUsedWhereThisMachineHasItAndRefusedWhereItDoesNot(GpuDevice device, string name)
    {
        var stage = Stage().OnDevice(device).WithSeed(1);

        if (!DevicePresence.Has(device))
        {
            var failure = Assert.Throws<InvalidOperationException>(stage.Build);
            output.WriteLine(failure.Message);
            Assert.Contains(name, failure.Message, StringComparison.Ordinal);
        }
        else
        {
            using var optimizer = stage.Build();
            output.WriteLine($"{name} present; got {optimizer.Device}");
            Assert.Equal(device, optimizer.Device.Kind);
            Assert.Null(optimizer.Device.FallbackReason);
        }
    }

    /// <summary>
    /// D1, under <c>Gpu</c>: an explicit <c>Cuda</c> gives the RTX 5070 Ti and an explicit
    /// <c>OpenCL</c> gives <c>gfx1036</c>, each with no fallback reason. Specific to the owner's
    /// machine.
    /// </summary>
    /// <param name="device">The device asked for.</param>
    /// <param name="expectedName">A part of the device name ILGPU reports.</param>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda, "RTX 5070 Ti")]
    [InlineData(GpuDevice.OpenCL, "gfx1036")]
    public void AnExplicitDeviceIsTheOwnersGpu(GpuDevice device, string expectedName)
    {
        using var optimizer = Stage().OnDevice(device).WithSeed(1).Build();
        output.WriteLine($"{device}: {optimizer.Device}");

        Assert.Equal(device, optimizer.Device.Kind);
        Assert.Contains(expectedName, optimizer.Device.Name, StringComparison.Ordinal);
        Assert.Null(optimizer.Device.FallbackReason);
    }

    /// <summary>A builder of the sum of squares in [−1, 1]², N = 8, one generation, to which only a device is left to say.</summary>
    /// <returns>The builder at its device stage.</returns>
    internal static IGpuDeviceRequired<SumOfSquares> Stage() =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(SumOfSquares))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(8)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(1);
}

/// <summary>Σ x_j², the objective the device cases build with; only the device is under test.</summary>
internal readonly struct SumOfSquares : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var sum = 0.0;
        for (var j = 0; j < genes.Length; j++)
        {
            sum += genes[j] * genes[j];
        }

        return sum;
    }
}
