using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Checks L5, L6, L7 and the device half of L9 of the GPU package's Devices/LibDevice/ACCEPTANCE.md: the post-link on the device, CUDA
/// without a toolkit, a bad library, and several CUDA contexts in one process. L6 is in
/// <see cref="DeviceAbsenceTests"/>, which runs everywhere and opens no device; the others are <c>Gpu</c> and name the owner's RTX 5070 Ti. After APThermo's
/// <c>BadLibraryTests</c> and <c>CudaWslDevicesTests</c> (commit <c>5fdd82c</c>).
/// </summary>
/// <param name="output">Receives the messages and the memory figures.</param>
public class CudaLibDeviceTests(ITestOutputHelper output)
{
    /// <summary>L7's bound on device memory lost to 20 failed opens: APThermo's for the same check.</summary>
    private const long LeakBoundBytes = 64L << 20;

    private const int FailedOpens = 20;

    private static readonly double[] Lower = [-1.0, -1.0];
    private static readonly double[] Upper = [1.0, 1.0];

    private static readonly MethodInfo ProbeKernel = typeof(MathProbe).GetMethod(nameof(MathProbe.Probe))!;

    /// <summary>
    /// L5: on the RTX 5070 Ti, the probe kernel's own PTX calls wrappers and defines none; <c>Link</c> compiles exactly
    /// those, defines every one, and the result loads. The lease itself was opened only after the same kernel loaded.
    /// </summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void OnTheRtx5070TiThePostLinkCompletesExactlyTheMissingWrappers()
    {
        using var lease = DeviceSelector.Open(Backend.Cuda);
        var cuda = Assert.IsType<CudaAccelerator>(lease.Accelerator);
        Assert.Contains("RTX 5070 Ti", cuda.Name, StringComparison.Ordinal);
        using var binding = cuda.BindScoped();

        var compiled = (PTXCompiledKernel)cuda.Backend.Compile(EntryPointDescription.FromImplicitlyGroupedKernel(ProbeKernel), KernelSpecialization.Empty);
        var called = LibDevicePostLink.WrappersCalled(compiled.PTXAssembly);
        Assert.NotEmpty(called);
        Assert.Empty(LibDevicePostLink.WrappersDefined(compiled.PTXAssembly));

        var result = LibDevicePostLink.Link(cuda, compiled);
        output.WriteLine($"{cuda.Name} ({cuda.Architecture}): compiled {string.Join(", ", result.Compiled)}");

        Assert.Empty(result.DefinedByIlgpu);
        Assert.Equal(called, result.Compiled);
        Assert.Equal(called.ToHashSet(StringComparer.Ordinal), LibDevicePostLink.WrappersDefined(compiled.PTXAssembly).ToHashSet(StringComparer.Ordinal));
        using var kernel = cuda.LoadAutoGroupedKernel(compiled);
    }

    /// <summary>
    /// L7: a file named as libnvvm that is not a library, with the real bitcode. An explicit CUDA request throws naming
    /// its path; 20 Auto opens fall back naming it and lose at most 64 MiB of free device memory, read on a CUDA lease
    /// opened normally.
    /// </summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void ABadLibraryIsNamedAndNeverReachesTheDevice()
    {
        var real = LibDeviceLocator.Locate();
        Assert.True(real.Found, "this check needs the machine's CUDA Toolkit for its bitcode");
        var directory = Directory.CreateTempSubdirectory("dotnet-de-gpu-bad-library-").FullName;
        try
        {
            var bogus = Path.Combine(directory, LibDeviceLocator.LibraryFileName);
            File.WriteAllText(bogus, "not a library");
            LibDeviceLocation Bogus() => new(bogus, real.Bitcode, [bogus, real.Bitcode!]);

            var refused = Assert.Throws<InvalidOperationException>(() => DeviceSelector.Open(Backend.Cuda, Bogus));
            output.WriteLine(refused.Message);
            Assert.Contains(bogus, refused.Message, StringComparison.Ordinal);

            using var reference = DeviceSelector.Open(Backend.Cuda);
            var before = FreeDeviceMemory(reference.Accelerator);
            for (var i = 0; i < FailedOpens; i++)
            {
                using var auto = DeviceSelector.Open(null, Bogus);
                Assert.NotEqual(Backend.Cuda, auto.Backend);
                Assert.Contains(bogus, auto.FallbackReason, StringComparison.Ordinal);
            }

            var after = FreeDeviceMemory(reference.Accelerator);
            output.WriteLine($"free device memory before {before >> 20} MiB, after {FailedOpens} failed CUDA opens {after >> 20} MiB");
            Assert.True(before - after <= LeakBoundBytes, $"{FailedOpens} failed CUDA opens cost {(before - after) >> 20} MiB of device memory");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>L9, on the device: three CUDA optimizers built one after another in this process each bind CUDA.</summary>
    [Fact]
    [Trait("Category", "Gpu")]
    public void EveryCudaOptimizerOfTheProcessBinds()
    {
        for (var i = 0; i < 3; i++)
        {
            using var optimizer = GpuDifferentialEvolutionBuilder.ForFunction(default(SumOfSquares))
                .WithBounds(Lower, Upper)
                .WithPopulationSize(8)
                .WithDefaultMutationStrategy(0.5, 0.9)
                .WithGenerationLimit(1)
                .OnDevice(GpuDevice.Cuda)
                .WithSeed(1)
                .Build();
            Assert.Equal(GpuDevice.Cuda, optimizer.Device.Kind);
        }
    }

    private static long FreeDeviceMemory(Accelerator accelerator)
    {
        using var binding = accelerator.BindScoped();
        Assert.Equal(CudaError.CUDA_SUCCESS, GetMemoryInfo(out var free, out _));
        return free;
    }

    /// <summary>Pins the <c>long</c> overload of <c>CudaAPI.GetMemoryInfo</c>, otherwise ambiguous with its <c>nint</c> sibling.</summary>
    private static CudaError GetMemoryInfo(out long free, out long total) => CudaAPI.CurrentAPI.GetMemoryInfo(out free, out total);
}
