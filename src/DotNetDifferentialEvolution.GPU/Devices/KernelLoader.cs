using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>
/// The one path every kernel of the package is loaded through, the bind-time probe included: compiled explicitly on every
/// backend, never through ILGPU's kernel cache (which hands two loads of one method the same <see cref="Kernel"/>); on CUDA
/// completed by <see cref="LibDevicePostLink"/>; on CUDA and OpenCL loaded with the group size <see cref="GroupSize"/> gives
/// for the kernel's largest extent, on the CPU accelerator with ILGPU's own grouping.
/// After APThermo's <c>KernelCache.Load</c> (<c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>).
/// </summary>
internal static class KernelLoader
{
    private static long LoadedKernels;

    /// <summary>Gets the number of kernels loaded through <see cref="Load"/> in this process (check A10).</summary>
    public static long LoadCount => Interlocked.Read(ref LoadedKernels);

    /// <summary>
    /// The group size for a kernel launched over at most <paramref name="extent"/> threads: the smallest multiple of the warp
    /// size that spreads the extent over every multiprocessor, at least one warp and at most the occupancy limit,
    /// <c>clamp(w·⌈⌈extent / m⌉ / w⌉, w, limit)</c> for warp size <c>w</c> and <c>m</c> multiprocessors.
    /// </summary>
    /// <param name="extent">The largest launch extent of the kernel, at least 1.</param>
    /// <param name="warpSize">The warp (wavefront) size <c>w</c>, at least 1.</param>
    /// <param name="multiprocessors">The number of multiprocessors <c>m</c>, at least 1.</param>
    /// <param name="occupancyLimit">The largest group size the kernel can be launched with, at least 1.</param>
    /// <returns>The group size.</returns>
    /// <exception cref="ArgumentOutOfRangeException">An argument is below 1.</exception>
    public static int GroupSize(int extent, int warpSize, int multiprocessors, int occupancyLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(extent, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(warpSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(multiprocessors, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(occupancyLimit, 1);

        var perMultiprocessor = ((long)extent + multiprocessors - 1) / multiprocessors;
        var warps = (perMultiprocessor + warpSize - 1) / warpSize;
        var spread = warps * warpSize;
        return (int)Math.Min(Math.Max(spread, warpSize), occupancyLimit);
    }

    /// <summary>Loads <paramref name="method"/>, implicitly grouped, on <paramref name="accelerator"/>.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="method">A closed kernel method.</param>
    /// <param name="extent">The largest launch extent of the kernel in the run, at least 1.</param>
    /// <returns>A kernel of its own; the caller disposes it.</returns>
    public static Kernel Load(Accelerator accelerator, MethodInfo method, int extent)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentOutOfRangeException.ThrowIfLessThan(extent, 1);

        var entry = EntryPointDescription.FromImplicitlyGroupedKernel(method);
        var specialization = KernelSpecialization.Empty;
        var compiled = accelerator.CompileKernel(in entry, in specialization);
        if (accelerator is CudaAccelerator cuda)
        {
            compiled = LibDevicePostLink.Link(cuda, (PTXCompiledKernel)compiled).Kernel;
        }

        var kernel = Place(accelerator, compiled, extent);
        _ = Interlocked.Increment(ref LoadedKernels);
        return kernel;
    }

    private static Kernel Place(Accelerator accelerator, CompiledKernel compiled, int extent)
    {
        if (accelerator.AcceleratorType == AcceleratorType.CPU)
        {
            return accelerator.LoadAutoGroupedKernel(compiled);
        }

        var automatic = accelerator.LoadAutoGroupedKernel(compiled);
        Kernel sized;
        try
        {
            var occupancyLimit = accelerator.EstimateGroupSize(automatic);
            var size = GroupSize(extent, accelerator.WarpSize, accelerator.NumMultiprocessors, occupancyLimit);
            if (size == occupancyLimit)
            {
                return automatic;
            }

            sized = accelerator.LoadImplicitlyGroupedKernel(compiled, size);
        }
        catch
        {
            automatic.Dispose();
            throw;
        }

        automatic.Dispose();
        return sized;
    }
}
