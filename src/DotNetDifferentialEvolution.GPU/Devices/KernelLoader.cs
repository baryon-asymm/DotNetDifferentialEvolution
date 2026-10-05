using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices.LibDevice;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace DotNetDifferentialEvolution.GPU.Devices;

/// <summary>
/// The one path every kernel of the package is loaded through, the bind-time probe included: on CUDA, compiled,
/// completed by <see cref="LibDevicePostLink"/> and loaded; on OpenCL and the CPU accelerator, loaded as ILGPU loads it.
/// After APThermo's <c>KernelCache.Load</c> (<c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>).
/// </summary>
internal static class KernelLoader
{
    /// <summary>Loads <paramref name="method"/>, implicitly grouped, on <paramref name="accelerator"/>.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="method">A closed kernel method.</param>
    /// <returns>The kernel; the caller disposes it.</returns>
    public static Kernel Load(Accelerator accelerator, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        if (accelerator is not CudaAccelerator cuda)
        {
            return accelerator.LoadAutoGroupedKernel(method);
        }

        var entry = EntryPointDescription.FromImplicitlyGroupedKernel(method);
        var compiled = (PTXCompiledKernel)cuda.Backend.Compile(entry, KernelSpecialization.Empty);
        return cuda.LoadAutoGroupedKernel(LibDevicePostLink.Link(cuda, compiled).Kernel);
    }
}
