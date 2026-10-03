# API.md — DotNetDifferentialEvolution.GPU/Devices/LibDevice

Namespace: `DotNetDifferentialEvolution.GPU.Devices.LibDevice`. Internal to the assembly;
visible to the GPU test project. Consumed by the parent node, `Devices`.

## Internal to the assembly ✅

```csharp
internal enum LocatorPlatform { Windows = 0, Linux = 1, Other = 2 }

internal sealed record LibDeviceLocation(string? Dll, string? Bitcode, IReadOnlyList<string> Tried)
{
    public bool Found { get; }
}

internal static class LibDeviceLocator
{
    public const string BitcodeName = "libdevice.10.bc";
    public static string LibraryFileName { get; }
    public static LibDeviceLocation Locate();
    internal static LibDeviceLocation Locate(LocatorPlatform platform,
        Func<string, string?> environment, string globRoot);
}

internal static class LibDevicePostLink
{
    public const string ExpectedIlgpuVersion = "1.5.3.0";
    public static string IlgpuVersion { get; }
    public static void AssertIlgpu();
    internal static (FieldInfo Fragments, FieldInfo Assembly) AssertIlgpu(string expectedVersion);
    public static IReadOnlyList<string> WrappersCalled(string ptx);
    public static IReadOnlyList<string> WrappersDefined(string ptx);
    public static LinkResult Link(CudaAccelerator accelerator, PTXCompiledKernel compiled);
    internal static void ThrowIfFailed(NvvmResult result, string call, string arch, string? log = null);
    internal static void ThrowIfFailed(CudaError result, string call, string arch, string? log = null);
    internal static void AssertEveryWrapperDefined(string body, IReadOnlyList<string> names);

    internal readonly record struct LinkResult(PTXCompiledKernel Kernel,
        IReadOnlyList<string> DefinedByIlgpu, IReadOnlyList<string> Compiled);
}

internal static class PtxText
{
    public const string WrapperNamePrefix = "__ilgpu__nv_";
    public static IEnumerable<string> CallSites(string ptx);
    public static IEnumerable<string> Definitions(string ptx);
    public static string? TargetSm(string ptx);
}

internal static class CudaWslDevices
{
    public static void Register(Context.Builder builder);
    internal static bool IsResolverAlreadySet(InvalidOperationException failure);
    internal static (PropertyInfo Registry, MethodInfo GetDevices) Reflect(
        string registryPropertyName, string getDevicesMethodName);
}
```

- `Locate()` reads the platform, the environment and the platform's base directory; the
  overload with three arguments is the seam the tests drive. `Tried` lists every library
  and bitcode path examined, in order; `Found` is true when both paths are set.
- `Link` trial-loads every kernel. When wrappers were missing it first compiles them,
  inserts them and replaces the kernel's PTX; a kernel that calls none comes back with
  both lists empty and its PTX unchanged. It throws `InvalidOperationException` when a
  wrapper has no fragment, the context has no libnvvm, libnvvm fails, or the driver refuses
  the result.
- `AssertIlgpu()` throws `InvalidOperationException` naming the loaded and the expected
  version, or the member that is missing or of another type.
- `PtxText` reads a PTX text as APThermo's three regular expressions do (`BOOT.md`): the
  wrapper names at `call` instructions, the wrapper names of `.visible`/`.weak .func`
  headers, both in order with repeats and the prefix kept, and the digits of the first
  `.target sm_XX` line, or `null`.
- `Register` registers the CUDA devices as `builder.Cuda()` does, under WSL too; a missing
  ILGPU member is an `InvalidOperationException` naming it.
- The members marked `internal` are seams for the tests (checks L1, L3, L4, L9).
