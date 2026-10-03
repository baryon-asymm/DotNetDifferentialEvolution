using System.Reflection;
using System.Runtime.InteropServices;
using ILGPU;
using ILGPU.Backends.PTX;
using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;

namespace DotNetDifferentialEvolution.GPU.Devices.LibDevice;

/// <summary>
/// Works around ILGPU 1.5.3's WSL defect (BOOT.md): <c>CudaContextExtensions.CudaInternal</c> calls
/// <c>NativeLibrary.SetDllImportResolver</c> on its own assembly every time it runs under WSL, and .NET allows one
/// resolver per assembly, so the second CUDA context of a process throws before any device is registered. Adapted from
/// APThermo (<c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>src/Execution/LibDevice/CudaWslDevices.cs</c>).
/// </summary>
internal static class CudaWslDevices
{
    private const string SetDllImportResolverName = "SetDllImportResolver";
    private const string RegistryPropertyName = "DeviceRegistry";
    private const string GetDevicesMethodName = "GetDevices";

    private static readonly Lazy<(PropertyInfo Registry, MethodInfo GetDevices)> Members = new(() => Reflect(RegistryPropertyName, GetDevicesMethodName));

    /// <summary>
    /// Registers the CUDA devices on <paramref name="builder"/> as the no-argument <c>builder.Cuda()</c> would. The public
    /// call is tried every time, so outside WSL, and for the first CUDA context of a process under WSL, it is the only path.
    /// When it throws the resolver failure, the resolver ILGPU needs is already in place, and the devices are registered
    /// through ILGPU's internal <c>CudaDevice.GetDevices(configure, predicate, registry)</c>, the call <c>CudaInternal</c>
    /// makes right after setting the resolver (ILGPU source, <c>CudaContextExtensions.cs</c>, tag v1.5.3).
    /// </summary>
    /// <param name="builder">The context builder.</param>
    public static void Register(Context.Builder builder)
    {
        try
        {
            _ = builder.Cuda();
        }
        catch (InvalidOperationException failure) when (IsResolverAlreadySet(failure))
        {
            var (registryProperty, getDevices) = Members.Value;
            var registry = registryProperty.GetValue(builder);
            _ = getDevices.Invoke(null, [new Action<CudaDeviceOverride>(_ => { }), new Predicate<CudaDevice>(AcceptsEveryDevice), registry]);
        }
    }

    /// <summary>
    /// Recognises the resolver-already-set failure by where it was thrown, not by its message, which an application
    /// trimmed with <c>UseSystemResourceKeys</c> replaces with a resource key.
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <returns><see langword="true"/> when <see cref="NativeLibrary"/>'s <c>SetDllImportResolver</c> threw it.</returns>
    internal static bool IsResolverAlreadySet(InvalidOperationException failure) =>
        failure.TargetSite?.Name == SetDllImportResolverName && failure.TargetSite.DeclaringType == typeof(NativeLibrary);

    /// <summary>
    /// Reflects the two ILGPU internals the workaround needs; a missing one names itself. The names are parameters so a test
    /// can hand it a wrong one without WSL.
    /// </summary>
    /// <param name="registryPropertyName">The name of <c>Context.Builder</c>'s device registry property.</param>
    /// <param name="getDevicesMethodName">The name of <c>CudaDevice</c>'s internal device enumeration.</param>
    /// <returns>The property and the method.</returns>
    /// <exception cref="InvalidOperationException">A member is missing or of another shape.</exception>
    internal static (PropertyInfo Registry, MethodInfo GetDevices) Reflect(string registryPropertyName, string getDevicesMethodName)
    {
        // Both members are internal to ILGPU, which grants this assembly no InternalsVisibleTo: only reflection reaches them.
        var registryProperty = typeof(Context.Builder).GetProperty(registryPropertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (registryProperty is null || registryProperty.PropertyType != typeof(DeviceRegistry))
        {
            throw new InvalidOperationException(
                $"ILGPU {LibDevicePostLink.IlgpuVersion}: Context.Builder.{registryPropertyName} is not the property the WSL workaround expects.");
        }

        var getDevices = typeof(CudaDevice).GetMethod(
            getDevicesMethodName,
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Action<CudaDeviceOverride>), typeof(Predicate<CudaDevice>), typeof(DeviceRegistry)],
            modifiers: null);
        return getDevices is null
            ? throw new InvalidOperationException(
                $"ILGPU {LibDevicePostLink.IlgpuVersion}: CudaDevice.{getDevicesMethodName}(Action<CudaDeviceOverride>, Predicate<CudaDevice>, DeviceRegistry) is not the method the WSL workaround expects.")
            : (registryProperty, getDevices);
    }

    /// <summary>The predicate the no-argument <c>Cuda()</c> passes: a known architecture and an instruction set the PTX backend supports.</summary>
    private static bool AcceptsEveryDevice(CudaDevice device) =>
        device.Architecture.HasValue && device.InstructionSet.HasValue
        && PTXCodeGenerator.SupportedInstructionSets.Contains(device.InstructionSet.Value);
}
