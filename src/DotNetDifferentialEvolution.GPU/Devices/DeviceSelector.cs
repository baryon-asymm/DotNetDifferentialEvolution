using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>
/// Opens the accelerator a run asks for. An explicit backend is that backend or an
/// <see cref="InvalidOperationException"/> naming it, never a fallback. Auto tries CUDA, then
/// OpenCL, then the CPU accelerator, and keeps the reason each skipped backend gave.
/// </summary>
internal static class DeviceSelector
{
    private static readonly Backend[] AutoOrder = [Backend.Cuda, Backend.OpenCL, Backend.Cpu];

    /// <summary>Opens <paramref name="requested"/>, or the first backend that opens when it is <see langword="null"/> (Auto).</summary>
    /// <param name="requested">The backend, or <see langword="null"/> for Auto.</param>
    /// <returns>A lease that owns the context and the accelerator.</returns>
    /// <exception cref="InvalidOperationException">The requested backend, or under Auto every backend, failed to open.</exception>
    public static AcceleratorLease Open(Backend? requested)
    {
        if (requested is { } backend)
        {
            return TryOpen(backend, null, out var lease, out var reason)
                ? lease
                : throw new InvalidOperationException($"The {NameOf(backend)} device was requested and cannot be used: {reason}");
        }

        var skipped = new List<string>();
        foreach (var candidate in AutoOrder)
        {
            var fallbackReason = skipped.Count == 0 ? null : string.Join("; ", skipped);
            if (TryOpen(candidate, fallbackReason, out var lease, out var reason))
            {
                return lease;
            }

            skipped.Add($"{NameOf(candidate)}: {reason}");
        }

        throw new InvalidOperationException("No device could be opened: " + string.Join("; ", skipped));
    }

    /// <summary>The name of a backend as messages and <c>GpuDeviceInfo</c> spell it.</summary>
    /// <param name="backend">The backend.</param>
    /// <returns>"CUDA", "OpenCL" or "CPU".</returns>
    public static string NameOf(Backend backend) => backend switch
    {
        Backend.Cuda => "CUDA",
        Backend.OpenCL => "OpenCL",
        Backend.Cpu => "CPU",
        _ => throw Undefined(backend),
    };

    private static bool TryOpen(
        Backend backend,
        string? fallbackReason,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AcceleratorLease? lease,
        out string reason)
    {
        Context? context = null;
        Accelerator? accelerator = null;
        try
        {
            context = Context.Create(builder => Configure(builder, backend));
            if (DeviceCount(context, backend) == 0)
            {
                lease = null;
                reason = "no such device is present.";
                return false;
            }

            accelerator = CreateAccelerator(context, backend);
            lease = AcceleratorLease.Owned(context, accelerator, backend, fallbackReason);
            context = null;
            accelerator = null;
            reason = string.Empty;
            return true;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            lease = null;
            reason = failure.Message;
            return false;
        }
        finally
        {
            accelerator?.Dispose();
            context?.Dispose();
        }
    }

    private static void Configure(Context.Builder builder, Backend backend)
    {
        _ = backend switch
        {
            Backend.Cuda => builder.Cuda(),
            Backend.OpenCL => builder.OpenCL(),
            Backend.Cpu => builder.CPU(),
            _ => throw Undefined(backend),
        };
        _ = builder.EnableAlgorithms();
    }

    private static int DeviceCount(Context context, Backend backend) => backend switch
    {
        Backend.Cuda => context.GetCudaDevices().Count,
        Backend.OpenCL => context.GetCLDevices().Count,
        Backend.Cpu => context.GetCPUDevices().Count,
        _ => throw Undefined(backend),
    };

    private static Accelerator CreateAccelerator(Context context, Backend backend) => backend switch
    {
        Backend.Cuda => context.CreateCudaAccelerator(0),
        Backend.OpenCL => context.CreateCLAccelerator(0),
        Backend.Cpu => context.CreateCPUAccelerator(0),
        _ => throw Undefined(backend),
    };

    private static ArgumentOutOfRangeException Undefined(Backend backend) =>
        new(nameof(backend), backend, "Not a defined backend.");
}
