# API.md — DotNetDifferentialEvolution.GPU/Objectives

Namespace: `DotNetDifferentialEvolution.GPU.Objectives`. What a caller implements to
put an objective on the device.

## Objective ✅

```csharp
public interface IGpuFitnessFunction
{
    double Evaluate(GeneView genes);
}
```

- **Shape.** Implemented by a struct, compiled into the kernel. `Evaluate` is called once
  per individual per launch, one GPU thread each.
- **Input and output.** `genes` is that individual's `D` genes, read-only. The return
  value is the fitness: lower is better, `NaN` is allowed and ranks worst.
- **Data.** Anything the objective needs it carries as fields: value types, and ILGPU
  `ArrayView`s allocated on the accelerator the optimizer runs on.
- **What the body may use.** Kernel code only: value types, no virtual calls, no
  allocation, no exceptions, no strings. ILGPU reports a violation when the optimizer is
  built, not when C# compiles it.
- **Visibility.** ILGPU emits its launchers into a dynamic assembly named
  `ILGPURuntime`. The objective type must be public, or internal in an assembly that
  declares `[assembly: InternalsVisibleTo("ILGPURuntime")]`; a private nested type
  fails at `Build` with ILGPU's "Access is denied".

## Gene view ✅

```csharp
public readonly struct GeneView : IEquatable<GeneView>
{
    public int Length { get; }
    public double this[int index] { get; }

    public bool Equals(GeneView other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(GeneView left, GeneView right);
    public static bool operator !=(GeneView left, GeneView right);
}
```

- A read-only window onto one individual's genes in device memory; built by the kernel,
  never by the caller (its constructor is internal).
- **No bounds check:** reading outside `0 ≤ index < Length` is undefined, since kernels
  cannot throw.
- Equality means the same window onto the same buffer (buffer, start and length); it is
  for host code and is not meaningful inside a kernel.
