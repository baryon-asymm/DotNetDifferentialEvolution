using ILGPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Test.Devices;

/// <summary>
/// Whether the machine running the tests has a CUDA or an OpenCL device, asked of ILGPU directly
/// and with the same context options the package's device selection uses, so the conditional
/// cases of D1 and B1 take the branch that matches the machine: on a hosted runner, the
/// no-device branch.
/// </summary>
internal static class DevicePresence
{
    /// <summary>Gets a value indicating whether ILGPU sees at least one CUDA device.</summary>
    public static bool HasCuda
    {
        get
        {
            using var context = Context.Create(builder => builder.Cuda());
            return context.GetCudaDevices().Count > 0;
        }
    }

    /// <summary>Gets a value indicating whether ILGPU sees at least one OpenCL device.</summary>
    public static bool HasOpenCL
    {
        get
        {
            using var context = Context.Create(builder => builder.OpenCL());
            return context.GetCLDevices().Count > 0;
        }
    }

    /// <summary>Whether ILGPU sees a device of <paramref name="device"/>'s backend.</summary>
    /// <param name="device">CUDA or OpenCL.</param>
    /// <returns><see langword="true"/> when one is present.</returns>
    public static bool Has(GpuDevice device) => device switch
    {
        GpuDevice.Cuda => HasCuda,
        GpuDevice.OpenCL => HasOpenCL,
        GpuDevice.Auto or GpuDevice.Cpu => throw new ArgumentOutOfRangeException(nameof(device), device, "Only CUDA and OpenCL can be absent."),
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Only CUDA and OpenCL can be absent."),
    };
}
