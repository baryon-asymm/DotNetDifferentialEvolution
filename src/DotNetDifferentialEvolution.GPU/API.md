# API.md — DotNetDifferentialEvolution.GPU

Namespace: `DotNetDifferentialEvolution.GPU`; the objective's contract is in
[Objectives](Objectives/API.md). A caller writes the objective as a struct, assembles a
run with the builder, and gets the best individual back as an `ISolution`. Everything not
listed here is internal structure and may change.

## Builder ✅

```csharp
public static class GpuDifferentialEvolutionBuilder
{
    public static IGpuBoundsRequired<TFunction> ForFunction<TFunction>(TFunction function)
        where TFunction : struct, IGpuFitnessFunction;
}

public interface IGpuBoundsRequired<TFunction> where TFunction : struct, IGpuFitnessFunction
{ IGpuPopulationSizeRequired<TFunction> WithBounds(ReadOnlyMemory<double> lowerBound, ReadOnlyMemory<double> upperBound); }

public interface IGpuPopulationSizeRequired<TFunction> where TFunction : struct, IGpuFitnessFunction
{ IGpuMutationStrategyRequired<TFunction> WithPopulationSize(int populationSize); }

public interface IGpuMutationStrategyRequired<TFunction> where TFunction : struct, IGpuFitnessFunction
{ IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability); }

public interface IGpuTerminationConditionRequired<TFunction> where TFunction : struct, IGpuFitnessFunction
{
    IGpuDeviceRequired<TFunction> WithGenerationLimit(int maxGenerations);
    IGpuDeviceRequired<TFunction> WithEvaluationLimit(long maxEvaluations);
}

public interface IGpuDeviceRequired<TFunction> where TFunction : struct, IGpuFitnessFunction
{
    IGpuDifferentialEvolutionBuilder<TFunction> OnDevice(GpuDevice device);
    IGpuDifferentialEvolutionBuilder<TFunction> OnAccelerator(Accelerator accelerator);
}

public interface IGpuDifferentialEvolutionBuilder<TFunction> where TFunction : struct, IGpuFitnessFunction
{
    IGpuDifferentialEvolutionBuilder<TFunction> WithSeed(int seed);
    IGpuDifferentialEvolutionBuilder<TFunction> WithPopulationUpdateHandler(
        IGpuPopulationUpdatedHandler handler, int everyNGenerations = 1);
    GpuDifferentialEvolution Build();
}

public enum GpuDevice { Auto = 0, Cuda = 1, OpenCL = 2, Cpu = 3 }
```

- **DE/rand/1/bin is the only scheme**, named as in the CPU builder.
- **The limits.** A generation limit runs exactly that many generations (≥ 1). An
  evaluation limit stops at the first generation boundary where the count, starting at
  N, is ≥ the limit; at least one generation runs.
- **Devices.**
  - `OnDevice(Auto)` tries CUDA, then OpenCL, then the CPU accelerator.
  - An explicit `Cuda`, `OpenCL` or `Cpu` uses that device or makes `Build` throw.
  - CUDA needs an installed CUDA Toolkit: its math is libdevice's (libnvvm and
    `libdevice.10.bc`, found through `CUDA_PATH` or the toolkit's default directories).
    Without one, an explicit `Cuda` throws and Auto skips CUDA with that reason.
  - `OnAccelerator` uses the caller's accelerator and never disposes it; the objective's
    own `ArrayView`s must live on it. For a CUDA accelerator, an objective that calls
    `Exp`, `Log` or `Pow` needs a context built with `LibDevice(libnvvm, libdevice)`;
    without it `Build` throws ILGPU's `InternalCompilerException` (measured 2026-10-03).
- **`Build`** opens the device, compiles the kernels, samples the population on the
  device and evaluates it: it costs N evaluations and the compile time. Kernel compile
  errors surface here.
- **Unseeded runs.** Without `WithSeed`, the seed is one draw of
  `RandomNumberGenerator.GetInt32(int.MaxValue)`.
- **The objective type** must be visible to ILGPU's runtime assembly
  ([Objectives](Objectives/API.md)).

## Optimizer and result ✅

```csharp
public sealed class GpuDifferentialEvolution : IDisposable
{
    public GpuDeviceInfo Device { get; }
    public Task<GpuOptimizationResult> RunAsync(CancellationToken cancellationToken = default);
    public void Dispose();
}

public sealed record GpuDeviceInfo(GpuDevice Kind, string Name, string? FallbackReason);

public sealed class GpuOptimizationResult : ISolution
{
    public ReadOnlyMemory<double> Genes { get; }
    public double FitnessFunctionValue { get; }
    public int Generations { get; }
    public long EvaluationCount { get; }
    public GpuDeviceInfo Device { get; }
}

public interface IGpuPopulationUpdatedHandler
{
    void Handle(GpuPopulationSnapshot snapshot);
}

public sealed class GpuPopulationSnapshot
{
    public int Generation { get; }
    public long EvaluationCount { get; }
    public int PopulationSize { get; }
    public int GenomeSize { get; }
    public ReadOnlyMemory<double> Genes { get; }                  // individual-major, N·D
    public ReadOnlyMemory<double> FitnessFunctionValues { get; }  // N
}
```

- **`RunAsync`** runs the generations on a thread of its own, bound to the accelerator,
  and returns at once.
  - The result is the best individual of the final population: `NaN` is worst, a tie
    goes to the lowest index.
  - The token is observed between generations and ends the task as canceled.
  - After the run, a second call returns the same task; during the run it throws.
- **`Device.FallbackReason`** says why `Auto` skipped each backend before the one it
  chose. It is `null` when nothing was skipped, when the device was explicit, and for a
  caller's accelerator.
- **The observer** gets a host copy of the population every `everyNGenerations`
  generations, on the run's thread. That copy is the only per-generation transfer, and
  the caller opts into it.
- **`Dispose`** stops a run in progress between generations and waits for it, then frees
  the device buffers, and the device unless it was the caller's. Called from the
  observer, it stops the run and the run's thread frees everything as it ends.

## Errors

| Situation | Behaviour |
|---|---|
| Bounds of different lengths, empty, not finite, or lower > upper | `ArgumentException` from `WithBounds` |
| `populationSize < 4` (rand/1 needs four distinct individuals), or N·D > `int.MaxValue` | `ArgumentOutOfRangeException` |
| `mutationForce` not finite or ≤ 0; `crossoverProbability` outside [0, 1] | `ArgumentOutOfRangeException` |
| A limit < 1 | `ArgumentOutOfRangeException` |
| `everyNGenerations < 1` | `ArgumentOutOfRangeException` |
| An undefined `GpuDevice` value | `ArgumentOutOfRangeException` from `OnDevice` |
| `null` handler or accelerator | `ArgumentNullException` |
| An accelerator other than CUDA, OpenCL or CPU | `ArgumentException` from `OnAccelerator` |
| An explicit device that is not present, or `Cuda` without a CUDA Toolkit | `InvalidOperationException` from `Build`, naming the device and the reason |
| The objective cannot be compiled by ILGPU | ILGPU's exception from `Build` |
| `RunAsync` while a run is in progress | `InvalidOperationException` |
| `RunAsync` after `Dispose` | `ObjectDisposedException` |
| The observer throws | the task faults with that exception |

## Side effects

`Build` opens a device context unless one is passed, allocates `3·N·D + 2·N + 2·D`
doubles on the device and compiles two kernels. A run copies the population to the host
once at the end, and once per observer call. No `GC.Collect`.

## Children

- [Objectives](Objectives/API.md) — the objective's contract and the gene view.
- [Devices](Devices/API.md) — device selection, ownership, the math probe (internal).
- [Kernels](Kernels/API.md) — the kernels and the DE step (internal).
- [Random](Random/API.md) — Philox4x32-10 and the draw conversions (internal).

## Out of scope

- `float`, jDE and the other adaptive variants, schemes other than DE/rand/1/bin.
- Stop rules other than the two limits, and a public termination interface.
- An objective on the host or through `IFitnessFunctionEvaluator`: a `ReadOnlySpan`
  cannot cross into an ILGPU kernel.
