using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using ILGPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Whether the machine running the tests can run CUDA or OpenCL, asked of ILGPU and of the package's libdevice locator
/// directly, so the conditional cases of D1, B1 and L6 take the branch that matches the machine: on a hosted runner, the
/// no-device branch.
/// </summary>
internal static class DevicePresence
{
    /// <summary>Gets a value indicating whether ILGPU sees at least one CUDA device, toolkit or not.</summary>
    public static bool HasCudaDevice
    {
        get
        {
            using var context = Context.Create(builder => builder.Cuda());
            return context.GetCudaDevices().Count > 0;
        }
    }

    /// <summary>Gets a value indicating whether CUDA can be opened: a CUDA device, and libnvvm and libdevice found.</summary>
    public static bool HasCuda => HasCudaDevice && LibDeviceLocator.Locate().Found;

    /// <summary>Gets a value indicating whether ILGPU sees at least one OpenCL device.</summary>
    public static bool HasOpenCL
    {
        get
        {
            using var context = Context.Create(builder => builder.OpenCL());
            return context.GetCLDevices().Count > 0;
        }
    }

    /// <summary>Whether <paramref name="device"/>'s backend can be opened here.</summary>
    /// <param name="device">CUDA or OpenCL.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    public static bool Has(GpuDevice device) => device switch
    {
        GpuDevice.Cuda => HasCuda,
        GpuDevice.OpenCL => HasOpenCL,
        GpuDevice.Auto or GpuDevice.Cpu => throw new ArgumentOutOfRangeException(nameof(device), device, "Only CUDA and OpenCL can be absent."),
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Only CUDA and OpenCL can be absent."),
    };
}
