using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;

namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>
/// Opens the accelerator a run asks for. An explicit backend is that backend or an
/// <see cref="InvalidOperationException"/> naming it, never a fallback. Auto tries CUDA, then
/// OpenCL, then the CPU accelerator, and keeps the reason each skipped backend gave. CUDA is
/// opened as APThermo opens it (<c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>src/Execution/AcceleratorChoice.cs</c>): libdevice found, libnvvm checked before the
/// accelerator exists, and the probe kernel loaded through the post-link before CUDA counts as
/// opened.
/// </summary>
internal static class DeviceSelector
{
    private static readonly Backend[] AutoOrder = [Backend.Cuda, Backend.OpenCL, Backend.Cpu];

    private static readonly MethodInfo ProbeKernel =
        typeof(MathProbe).GetMethod(nameof(MathProbe.Probe), BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>Opens <paramref name="requested"/>, or the first backend that opens when it is <see langword="null"/> (Auto).</summary>
    /// <param name="requested">The backend, or <see langword="null"/> for Auto.</param>
    /// <returns>A lease that owns the context and the accelerator.</returns>
    /// <exception cref="InvalidOperationException">The requested backend, or under Auto every backend, failed to open.</exception>
    public static AcceleratorLease Open(Backend? requested) => Open(requested, LibDeviceLocator.Locate);

    /// <summary>The same, with libdevice found by <paramref name="locate"/>: the seam of checks L6 and L7.</summary>
    /// <param name="requested">The backend, or <see langword="null"/> for Auto.</param>
    /// <param name="locate">Finds libnvvm and libdevice; asked only when CUDA is tried.</param>
    /// <returns>A lease that owns the context and the accelerator.</returns>
    /// <exception cref="InvalidOperationException">The requested backend, or under Auto every backend, failed to open.</exception>
    internal static AcceleratorLease Open(Backend? requested, Func<LibDeviceLocation> locate)
    {
        if (requested is { } backend)
        {
            return TryOpen(backend, null, locate, out var lease, out var reason)
                ? lease
                : throw new InvalidOperationException($"The {NameOf(backend)} device was requested and cannot be used: {reason}");
        }

        var skipped = new List<string>();
        foreach (var candidate in AutoOrder)
        {
            var fallbackReason = skipped.Count == 0 ? null : string.Join("; ", skipped);
            if (TryOpen(candidate, fallbackReason, locate, out var lease, out var reason))
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
        Func<LibDeviceLocation> locate,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AcceleratorLease? lease,
        out string reason)
    {
        Context? context = null;
        Accelerator? accelerator = null;
        try
        {
            LibDeviceLocation? location = null;
            if (backend == Backend.Cuda)
            {
                LibDevicePostLink.AssertIlgpu();
                location = locate();
            }

            context = Context.Create(builder => Configure(builder, backend, location));
            if (DeviceCount(context, backend) == 0)
            {
                lease = null;
                reason = "no such device is present.";
                return false;
            }

            if (location is { Found: false })
            {
                lease = null;
                reason = NotFound(location);
                return false;
            }

            if (location is { Dll: { } dll, Bitcode: { } bitcode })
            {
                // The library before the device: a bad one never reaches ILGPU's accelerator constructor, which would
                // create a CUDA context first and keep no handle to release it.
                CheckLibraries(dll, bitcode);
            }

            accelerator = CreateAccelerator(context, backend);
            if (backend == Backend.Cuda)
            {
                ProbeBinding(accelerator);
            }

            lease = AcceleratorLease.Owned(context, accelerator, backend, fallbackReason);
            context = null;
            accelerator = null;
            reason = string.Empty;
            return true;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            lease = null;
            reason = failure.Message + ReleaseAfterFailure(ref accelerator, ref context);
            return false;
        }
        finally
        {
            accelerator?.Dispose();
            context?.Dispose();
        }
    }

    /// <summary>
    /// Releases what a failed open created, so that a failure of the release cannot replace the reason the open failed:
    /// ILGPU 1.5.3's CUDA accelerator throws from <c>Dispose</c> after a kernel failed to load (measured 2026-10-03,
    /// "invalid resource handle"). A release failure is appended to the reason, not dropped.
    /// </summary>
    /// <returns>Nothing, or "; releasing it also failed: …".</returns>
    private static string ReleaseAfterFailure(ref Accelerator? accelerator, ref Context? context)
    {
        var failures = new List<string>();
        try
        {
            accelerator?.Dispose();
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            failures.Add(failure.Message);
        }

        accelerator = null;
        try
        {
            context?.Dispose();
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            failures.Add(failure.Message);
        }

        context = null;
        return failures.Count == 0 ? string.Empty : "; releasing it also failed: " + string.Join("; ", failures);
    }

    /// <summary>
    /// One backend per context. CUDA registers its devices through <see cref="CudaWslDevices"/> and, when libdevice was
    /// found, gets <c>Math(MathMode.Default)</c> and <c>LibDevice</c>, so ILGPU emits the wrapper calls the post-link
    /// completes. OpenCL and the CPU accelerator use their own math.
    /// </summary>
    private static void Configure(Context.Builder builder, Backend backend, LibDeviceLocation? location)
    {
        switch (backend)
        {
            case Backend.Cuda:
                CudaWslDevices.Register(builder);
                if (location is { Dll: { } dll, Bitcode: { } bitcode })
                {
                    _ = builder.Math(MathMode.Default).LibDevice(dll, bitcode);
                }

                break;
            case Backend.OpenCL:
                _ = builder.OpenCL();
                break;
            case Backend.Cpu:
                _ = builder.CPU();
                break;
            default:
                throw Undefined(backend);
        }
    }

    private static string NotFound(LibDeviceLocation location) =>
        $"libnvvm ({LibDeviceLocator.LibraryFileName}) and libdevice ({LibDeviceLocator.BitcodeName}) of a CUDA Toolkit were not found"
        + (location.Tried.Count == 0
            ? "; there was no CUDA_PATH and no toolkit directory to look in."
            : $"; tried {string.Join(", ", location.Tried)}.");

    /// <summary>Loads libnvvm, asks its IR version and reads the bitcode, then releases them; a failure names both paths.</summary>
    private static void CheckLibraries(string dll, string bitcode)
    {
        try
        {
            using var nvvm = NvvmAPI.Create(dll, bitcode);
            var result = nvvm.GetIRVersion(out _, out _, out _, out _);
            if (result != NvvmResult.NVVM_SUCCESS)
            {
                throw new InvalidOperationException($"libnvvm's GetIRVersion returned {result}.");
            }

            _ = nvvm.LibDeviceBytes.Length;
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            throw new InvalidOperationException($"libnvvm ({dll}) or libdevice ({bitcode}) could not be loaded: {failure.Message}", failure);
        }
    }

    /// <summary>CUDA counts as opened only when a kernel loads on it: the math probe, through the post-link, released at once.</summary>
    private static void ProbeBinding(Accelerator accelerator)
    {
        try
        {
            using var binding = accelerator.BindScoped();
            using var probe = KernelLoader.Load(accelerator, ProbeKernel);
        }
        catch (Exception failure) when (failure is not OutOfMemoryException)
        {
            throw new InvalidOperationException($"the math probe kernel could not be loaded: {failure.Message}", failure);
        }
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
