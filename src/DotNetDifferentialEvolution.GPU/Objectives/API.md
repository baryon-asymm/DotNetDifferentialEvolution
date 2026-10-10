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

## Pointwise objective ✅

Designed 2026-10-09 ([HISTORY.md](../HISTORY.md#pointwise-decided-2026-10-09)), for 1.1.0.

```csharp
public interface IGpuPointwiseFitnessFunction<TPoint> where TPoint : unmanaged
{
    TPoint EvaluatePoint(GeneView genes, int point);
    double Combine(GeneView genes, PointView<TPoint> points);
}

public readonly struct PointView<TPoint> : IEquatable<PointView<TPoint>> where TPoint : unmanaged
{
    public int Length { get; }
    public TPoint this[int index] { get; }

    public bool Equals(PointView<TPoint> other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(PointView<TPoint> left, PointView<TPoint> right);
    public static bool operator !=(PointView<TPoint> left, PointView<TPoint> right);
}
```

- **What it is for.** An objective made of `P` independent parts (experimental points,
  load cases, scenarios) and a combination of their results. Written as
  `IGpuFitnessFunction`, one thread computes all `P` parts, and a generation lasts as long
  as that thread. Written this way, the package evaluates the `N·P` parts in `N·P`
  threads and combines them per individual. `P` is fixed when the run is built
  (`GpuDifferentialEvolutionBuilder.ForPointwiseFunction`, package root `API.md`).
- **`EvaluatePoint`** is called once per individual and point, each call in its own GPU
  thread, in no particular order. `genes` is the individual's genes and
  `0 ≤ point < P`. It returns the point's result: an unmanaged struct (a value; several
  values; a value and a flag). ⏳ 2026-10-10 (A5): of sequential layout, with fields of
  primitive numeric types (not `bool` or `char`), their enums or such structs, and no
  packing below its natural size; `ForPointwiseFunction` refuses any other.
- **`Combine`** is called once per individual, in one thread, after all its points:
  `points[p]` is what `EvaluatePoint(genes, p)` returned for that individual. It returns
  the fitness, with `IGpuFitnessFunction`'s meaning: lower is better, `NaN` is allowed
  and ranks worst. The package never reorders, adds or otherwise touches the point
  results; a `Combine` that reads them in a fixed order makes the fitness the same bits on
  every launch.
- **The same run as a monolithic objective.** A pointwise objective whose `EvaluatePoint`
  and `Combine` perform the arithmetic of an `IGpuFitnessFunction`, operation for
  operation and in the same order, gives the same run bit for bit on the CPU accelerator
  (Kernels `ACCEPTANCE.md`, check P1). ⚠ 2026-10-10: was "bit for bit" on every device; on
  a GPU the device compiler may fuse a multiply and an add of the monolithic form that the
  pointwise form stores, so values can differ in the last bits →
  [HISTORY.md](../HISTORY.md#audit-fixes-decided-2026-10-10).
- **Counting.** One evaluation is one individual: its `P` points and its `Combine`.
  Evaluation limits and `EvaluationCount` count individuals, as for
  `IGpuFitnessFunction`.
- **Memory.** The package keeps the `N·P` point results on the device:
  `N·P·sizeof(TPoint)` bytes besides the population.
- **Data, body and visibility**: as for `IGpuFitnessFunction` above; `TPoint` too must be
  visible to ILGPU's runtime assembly.
- **`PointView`** is to the point results what `GeneView` is to the genes: a read-only
  window, built by the kernel (its constructor is internal), without bounds checks.
