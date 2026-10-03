# API.md — DotNetDifferentialEvolution.GPU/Devices

Namespace: `DotNetDifferentialEvolution.GPU.Devices`. Internal to the assembly; visible
to the GPU test project and to ILGPU's runtime assembly. Consumed by the package root.

## Internal to the assembly ✅

```csharp
internal enum Backend { Cuda = 0, OpenCL = 1, Cpu = 2 }

internal sealed class AcceleratorLease : IDisposable
{
    public Accelerator Accelerator { get; }
    public Backend Backend { get; }
    public string? FallbackReason { get; }

    public static AcceleratorLease Owned(Context context, Accelerator accelerator,
        Backend backend, string? fallbackReason);
    public static AcceleratorLease Borrowed(Accelerator accelerator);
    public void Dispose();
}

internal static class DeviceSelector
{
    public static AcceleratorLease Open(Backend? requested);
    internal static AcceleratorLease Open(Backend? requested, Func<LibDeviceLocation> locate);
    public static string NameOf(Backend backend);
}

internal static class KernelLoader
{
    public static Kernel Load(Accelerator accelerator, MethodInfo method);
}

internal static class MathProbe
{
    public const double PowExponent = 1.37;
    public const int FunctionCount = 4;

    public static void Probe(Index1D index, ArrayView<double> inputs,
        ArrayView<double> outputs);
}
```

- `Open(null)` is Auto: CUDA, OpenCL, CPU, the first that opens. `FallbackReason` joins
  `"<backend>: <reason>"` for each skipped one with `"; "`. `Open(backend)` opens that one
  or throws `InvalidOperationException` ("The CUDA device was requested and cannot be
  used: …"). CUDA opens only with libnvvm and libdevice found and the probe kernel loaded
  (`BOOT.md`, Constraints); without a toolkit the reason is "libnvvm (nvvm64_40_0.dll) and
  libdevice (libdevice.10.bc) of a CUDA Toolkit were not found; …". The overload with
  `locate` is the seam of checks L6 and L7.
- `Borrowed` throws `ArgumentException` for an accelerator other than CUDA, OpenCL or
  CPU; its lease never disposes the accelerator.
- `Load` returns the kernel, implicitly grouped, for a closed kernel method; the caller
  disposes it. On a `CudaAccelerator` it throws what the compile or the post-link throws
  ([LibDevice](LibDevice/API.md)).
- `Probe`: thread i writes `Exp(x)`, `Log(x)`, `Pow(x, 1.37)`, `Sqrt(x)` of input i to
  outputs `4i … 4i+3` (after APT's `src/Execution/MathProbe.cs`).
