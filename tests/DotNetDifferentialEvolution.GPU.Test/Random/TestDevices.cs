using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// Opens an ILGPU context and its first accelerator of one backend, for the kernels these tests
/// launch directly. Absent hardware is a failure, not a skip: a <c>Category=Gpu</c> test runs only
/// where the device is (BOOT.md).
/// </summary>
internal static class TestDevices
{
    /// <summary>A context with the one backend of <paramref name="device"/> and ILGPU.Algorithms enabled.</summary>
    /// <param name="device"><see cref="GpuDevice.Cpu"/>, <see cref="GpuDevice.Cuda"/> or <see cref="GpuDevice.OpenCL"/>.</param>
    /// <returns>The context; the caller disposes it.</returns>
    public static Context CreateContext(GpuDevice device) => device switch
    {
        GpuDevice.Cuda => Context.Create(builder => builder.Cuda().EnableAlgorithms()),
        GpuDevice.OpenCL => Context.Create(builder => builder.OpenCL().EnableAlgorithms()),
        GpuDevice.Cpu => Context.Create(builder => builder.CPU()),
        GpuDevice.Auto => throw new ArgumentOutOfRangeException(nameof(device), device, "Not a single backend."),
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Not a defined backend."),
    };

    /// <summary>The first accelerator of <paramref name="device"/>'s backend in <paramref name="context"/>.</summary>
    /// <param name="context">A context from <see cref="CreateContext"/>.</param>
    /// <param name="device">The same backend.</param>
    /// <returns>The accelerator; the caller disposes it.</returns>
    public static Accelerator CreateAccelerator(Context context, GpuDevice device) => device switch
    {
        GpuDevice.Cuda => context.CreateCudaAccelerator(0),
        GpuDevice.OpenCL => context.CreateCLAccelerator(0),
        GpuDevice.Cpu => context.CreateCPUAccelerator(0),
        GpuDevice.Auto => throw new ArgumentOutOfRangeException(nameof(device), device, "Not a single backend."),
        _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Not a defined backend."),
    };
}
