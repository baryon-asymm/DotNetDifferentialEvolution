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
    public static string NameOf(Backend backend);
}

internal static class MathProbe
{
    public const double PowExponent = 1.37;
    public const int FunctionCount = 4;

    public static void Probe(Index1D index, ArrayView<double> inputs,
        ArrayView<double> outputs);
}
```

- `Open(null)` is Auto: CUDA, OpenCL, CPU, the first whose context lists a device and
  whose accelerator is created. `FallbackReason` joins `"<backend>: <reason>"` for each
  skipped one with `"; "`. `Open(backend)` opens that one or throws
  `InvalidOperationException` ("The CUDA device was requested and cannot be used: …").
- `Borrowed` throws `ArgumentException` for an accelerator other than CUDA, OpenCL or
  CPU; its lease never disposes the accelerator.
- `Probe`: thread i writes `Exp(x)`, `Log(x)`, `Pow(x, 1.37)`, `Sqrt(x)` of input i to
  outputs `4i … 4i+3` (after APT's `src/Execution/MathProbe.cs`).
