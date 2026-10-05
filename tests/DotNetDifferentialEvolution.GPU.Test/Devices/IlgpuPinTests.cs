using System.Reflection;
using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Check L4 of the GPU package's Devices/LibDevice/ACCEPTANCE.md, the ILGPU pin, and the part of L9 that runs without a GPU: the ILGPU
/// internals the post-link and the WSL workaround read are asserted and fail loudly, by name. After APThermo's
/// <c>CudaWslDevicesTests</c> (commit <c>5fdd82c</c>).
/// </summary>
public class IlgpuPinTests
{
    /// <summary>L4: on the referenced ILGPU the version and both reflected members are as the post-link expects.</summary>
    [Fact]
    public void TheReferencedIlgpuPassesTheAssertion()
    {
        var (fragments, assembly) = LibDevicePostLink.AssertIlgpu(LibDevicePostLink.ExpectedIlgpuVersion);

        Assert.Equal(LibDevicePostLink.ExpectedIlgpuVersion, LibDevicePostLink.IlgpuVersion);
        Assert.NotNull(fragments);
        Assert.NotNull(assembly);
    }

    /// <summary>L4: for another expected version the assertion throws, naming the loaded version and the expected one.</summary>
    [Fact]
    public void AnotherExpectedVersionThrowsNamingBoth()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => LibDevicePostLink.AssertIlgpu("9.9.9.0"));

        Assert.Contains("9.9.9.0", failure.Message, StringComparison.Ordinal);
        Assert.Contains(LibDevicePostLink.IlgpuVersion, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>L4: the WSL workaround's reflection names a member it cannot find; the real names resolve.</summary>
    [Fact]
    public void TheWslReflectionNamesAMissingMember()
    {
        var registry = Assert.Throws<InvalidOperationException>(() => CudaWslDevices.Reflect("NoSuchRegistryProperty", "GetDevices"));
        Assert.Contains("NoSuchRegistryProperty", registry.Message, StringComparison.Ordinal);

        var getDevices = Assert.Throws<InvalidOperationException>(() => CudaWslDevices.Reflect("DeviceRegistry", "NoSuchGetDevicesMethod"));
        Assert.Contains("NoSuchGetDevicesMethod", getDevices.Message, StringComparison.Ordinal);

        var (property, method) = CudaWslDevices.Reflect("DeviceRegistry", "GetDevices");
        Assert.NotNull(property);
        Assert.NotNull(method);
    }

    /// <summary>
    /// L9: a real second <c>SetDllImportResolver</c> on one assembly throws the failure ILGPU's resolver install throws
    /// under WSL, and it is recognised; an exception with the same text thrown from elsewhere is not. The assembly is a
    /// fresh copy on disk, loaded on its own, since a resolver cannot be removed once set.
    /// </summary>
    [Fact]
    public void TheResolverFailureIsRecognisedByWhereItWasThrownNotByItsMessage()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "DotNetOptimization.Abstractions.dll");
        var copy = Path.Combine(Path.GetTempPath(), $"DotNetOptimization.Abstractions.{Guid.NewGuid():N}.dll");
        File.Copy(source, copy);
        var assembly = Assembly.LoadFile(copy);
        NativeLibrary.SetDllImportResolver(assembly, (_, _, _) => IntPtr.Zero);

        var real = Assert.Throws<InvalidOperationException>(() => NativeLibrary.SetDllImportResolver(assembly, (_, _, _) => IntPtr.Zero));

        Assert.True(CudaWslDevices.IsResolverAlreadySet(real));
        Assert.False(CudaWslDevices.IsResolverAlreadySet(new InvalidOperationException(real.Message)));
    }
}
