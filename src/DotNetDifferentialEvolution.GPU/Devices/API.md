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
    internal static AcceleratorLease Open(Backend? requested, Func<LibDeviceLocation> locate,
        Func<Backend, bool>? isPresent);
    public static string NameOf(Backend backend);
}

internal static class KernelLoader
{
    public static Kernel Load(Accelerator accelerator, MethodInfo method, int extent);
    public static int GroupSize(int extent, int warpSize, int multiprocessors, int occupancyLimit);
    public static long LoadCount { get; }   // loads in this process, for A10
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
  `locate` is the seam of checks L6 and L7. The overload with `isPresent` (check A12) decides
  whether a backend has a device instead of asking ILGPU, and `null` asks ILGPU, as the
  shorter overloads do: a backend it denies is skipped, or refused when explicit, with "no
  such device is present." before any context for it exists, so no driver is loaded; for CUDA
  the toolkit is checked next, still before a context, with the reason above; a backend it
  affirms opens as usual. The package root's `GpuBuilder` passes it from its internal
  `WithDevicePresence(Func<Backend, bool>)`, beside `WithStopReadInterval`, which tests reach by
  casting the builder; no public member changes.
- `Borrowed` throws `ArgumentException` for an accelerator other than CUDA, OpenCL or
  CPU; its lease never disposes the accelerator.
- `Load` returns the kernel, implicitly grouped, for a closed kernel method; the caller
  disposes it. It compiles the method explicitly on every backend (`CompileKernel` of the
  implicitly grouped entry point), never through ILGPU's kernel cache: two loads return two
  `Kernel` objects, and disposing one leaves the other alone (check A1). On CUDA the
  post-link follows. On CUDA and OpenCL it loads with
  `GroupSize(extent, WarpSize, NumMultiprocessors, ILGPU's occupancy estimate)`; on the CPU
  accelerator with ILGPU's own grouping. `extent` is the largest launch extent of the kernel
  in the run (N_init, N_init·P, a chunk count, a sort length; for `MathProbe` the probe's;
  for the bind-time probe, which is never launched, 1); below 1 it throws
  `ArgumentOutOfRangeException`. On a `CudaAccelerator` it throws what the compile or the
  post-link throws ([LibDevice](LibDevice/API.md)).
- `GroupSize` is `clamp(w·⌈⌈extent / m⌉ / w⌉, w, occupancyLimit)` with `w = warpSize`,
  `m = multiprocessors`; an argument below 1 throws `ArgumentOutOfRangeException` (check A2).
- `LoadCount` counts the loads that succeeded, the bind-time probe's included; it is
  process-wide, so a test that reads it runs in the `KernelLoadCount` collection (check A10).
- `Probe`: thread i writes `Exp(x)`, `Log(x)`, `Pow(x, 1.37)`, `Sqrt(x)` of input i to
  outputs `4i … 4i+3` (after APT's `src/Execution/MathProbe.cs`).

## Audit fixes ⏳

Designed 2026-10-10 ([HISTORY.md](../HISTORY.md#audit-fixes-decided-2026-10-10)), check A6;
checks A1, A2 and A10 are built, above.

- `AcceleratorLease.Dispose` disposes the owned context in a `finally`; when the accelerator's
  `Dispose` throws, that exception propagates after the context is released.
