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

public interface IGpuBoundsRequired<TFunction> where TFunction : struct
{ IGpuPopulationSizeRequired<TFunction> WithBounds(ReadOnlyMemory<double> lowerBound, ReadOnlyMemory<double> upperBound); }

public interface IGpuPopulationSizeRequired<TFunction> where TFunction : struct
{ IGpuMutationStrategyRequired<TFunction> WithPopulationSize(int populationSize); }

public interface IGpuMutationStrategyRequired<TFunction> where TFunction : struct
{ IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability); }

public interface IGpuTerminationConditionRequired<TFunction> where TFunction : struct
{
    IGpuDeviceRequired<TFunction> WithGenerationLimit(int maxGenerations);
    IGpuDeviceRequired<TFunction> WithEvaluationLimit(long maxEvaluations);
}

public interface IGpuDeviceRequired<TFunction> where TFunction : struct
{
    IGpuDifferentialEvolutionBuilder<TFunction> OnDevice(GpuDevice device);
    IGpuDifferentialEvolutionBuilder<TFunction> OnAccelerator(Accelerator accelerator);
}

public interface IGpuDifferentialEvolutionBuilder<TFunction> where TFunction : struct
{
    IGpuDifferentialEvolutionBuilder<TFunction> WithSeed(int seed);
    IGpuDifferentialEvolutionBuilder<TFunction> WithPopulationUpdateHandler(
        IGpuPopulationUpdatedHandler handler, int everyNGenerations = 1);
    GpuDifferentialEvolution Build();
}

public enum GpuDevice { Auto = 0, Cuda = 1, OpenCL = 2, Cpu = 3 }
```

- **DE/rand/1/bin**, named as in the CPU builder; the other schemes and the variants
  below.
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
    So do JADE, SHADE and L-SHADE, whose samplers call `Log`, `Cos` and `Tan`: without
    `LibDevice` their `Build` throws the same, while the fixed schemes and jDE build and run
    (measured 2026-10-05, RTX 5070 Ti, a scratch program outside the tree).
- **`Build`** opens the device, compiles the kernels, samples the population on the
  device and evaluates it: it costs N evaluations and the compile time. Kernel compile
  errors surface here.
- **Unseeded runs.** Without `WithSeed`, the seed is one draw of
  `RandomNumberGenerator.GetInt32(int.MaxValue)`.
- **The objective type** must be visible to ILGPU's runtime assembly
  ([Objectives](Objectives/API.md)).

## Pointwise objective ✅

Designed 2026-10-09 ([HISTORY.md](HISTORY.md#pointwise-decided-2026-10-09)), built for 1.1.0
(checks P1–P5 of the Kernels [ACCEPTANCE.md](Kernels/ACCEPTANCE.md)).

```csharp
public static class GpuDifferentialEvolutionBuilder
{
    public static IGpuBoundsRequired<TFunction> ForPointwiseFunction<TFunction, TPoint>(
        TFunction function, int pointCount)
        where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
        where TPoint : unmanaged;
}
```

- **The rest of the builder is shared.** Bounds, population size, every scheme and
  variant, every stop rule, device, seed and observer are the same calls. The six stage
  interfaces (`IGpuBoundsRequired<TFunction>` to `IGpuDifferentialEvolutionBuilder<TFunction>`)
  relax their constraint from `struct, IGpuFitnessFunction` to `struct` so that both entry
  points return them; code written against 1.0 compiles and runs unchanged.
- **Both type arguments are written at the call**: C# infers `TFunction` from the argument but
  not `TPoint` from the constraint, as in `ForPointwiseFunction<MyObjective, MyPoint>(f, 12)`.
- **`pointCount`** is `P ≥ 1`, else `ForPointwiseFunction` throws
  `ArgumentOutOfRangeException`. One thread per point: `WithPopulationSize` refuses
  `N·P > int.MaxValue` as it refuses `N·D > int.MaxValue`, with the same exception.
- **The run.** Initialisation and each generation are three launches instead of one
  (Kernels `BOOT.md`); everything between generations (best index, ranking, archive,
  adaptation, reduction, stop rule, observer) is the same. Results equal a monolithic
  objective's with the same arithmetic, bit for bit on the CPU accelerator (Kernels
  `ACCEPTANCE.md`, P1; ⚠ 2026-10-10: was on every device, see `## Audit fixes ⏳`).
- **Not in the CPU package.** On the host an objective computes its parts itself; the
  split exists because on the device one thread per individual is one thread for all
  its parts.

## Symmetry with the CPU package ✅

Designed and built 2026-10-05 (HISTORY.md#symmetry-decided-2026-10-05), checks S1–S17.

```csharp
public interface IGpuMutationStrategyRequired<TFunction> where TFunction : struct
{
    IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability);
    IGpuTerminationConditionRequired<TFunction> WithBestMutationStrategy(double mutationForce, double crossoverProbability);
    IGpuTerminationConditionRequired<TFunction> WithCurrentToBestMutationStrategy(double mutationForce, double crossoverProbability);
    IGpuTerminationConditionRequired<TFunction> WithRandTwoMutationStrategy(double mutationForce, double crossoverProbability);
    IGpuTerminationConditionRequired<TFunction> WithBestTwoMutationStrategy(double mutationForce, double crossoverProbability);
    IGpuTerminationConditionRequired<TFunction> WithJde(double initialMutationForce = 0.5, double initialCrossoverProbability = 0.9);
    IGpuTerminationConditionRequired<TFunction> WithJade(double pBestRate = 0.1, double archiveSizeRate = 1.0, double adaptationRate = 0.1);
    IGpuTerminationConditionRequired<TFunction> WithShade(double pBestRate = 0.2, double archiveSizeRate = 1.0, int memorySize = 100);
    IGpuTerminationConditionRequired<TFunction> WithLShade(long maxEvaluationNumber, double pBestRate = 0.11,
        double archiveSizeRate = 2.6, int memorySize = 6);
}

public interface IGpuTerminationConditionRequired<TFunction> where TFunction : struct
{
    IGpuDeviceRequired<TFunction> WithGenerationLimit(int maxGenerations);
    IGpuDeviceRequired<TFunction> WithEvaluationLimit(long maxEvaluations);
    IGpuDeviceRequired<TFunction> WithStagnationLimit(int maxStagnationStreak, double stagnationThreshold);
}
```

- **The CPU builder's names, parameters, order and defaults** (check S1), and its
  semantics (`docs/ALGORITHMS.md` §§3–7, §9): each fixed scheme with F and CR and
  ties accepted; jDE on rand/1, F and CR per individual, inherited on survival, ties
  refused (Brest et al. 2006; since 2026-10-06); JADE on current-to-pbest/1 with an
  archive, ties refused; SHADE with a success-history memory
  and p drawn from [min(2/N, p), p]; L-SHADE with the Lehmer CR mean, the terminal CR
  and linear population reduction to 4 at `maxEvaluationNumber`.
- **The minimum population** is the scheme's, as in the CPU package: rand/1, jDE, JADE,
  SHADE and L-SHADE 4; best/1 and current-to-best/1 3; rand/2 6; best/2 5.
- **The stagnation limit** is the CPU package's `StagnationStreakTerminationStrategy`:
  after each generation, if `|best − last| > threshold` then `last = best` and the streak
  is 0, else the streak grows; the run stops when the streak reaches `maxStagnationStreak`.
  `last` starts at `double.MinValue`. A cancellation after the rule has fired, before the
  run has read it, still completes with the result at the stopping generation, as the CPU
  package, which tests its stop rule before its cancellation (S18).
- **L-SHADE** with an evaluation limit other than `maxEvaluationNumber` makes `Build`
  throw, as the CPU package's `LShadeVariant.Validate` does; with a generation or a
  stagnation limit it runs, and its population reaches 4 at the budget.
- **The observer** sees the current population size: under L-SHADE it shrinks.

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
| `populationSize < 1`, or N·D > `int.MaxValue` ⚠ 2026-10-05: was `populationSize < 4`, now each scheme's minimum is checked by `Build` (next row) → HISTORY.md#symmetry-decided-2026-10-05 | `ArgumentOutOfRangeException` |
| N below the scheme's minimum (L-SHADE: below 4) | `InvalidOperationException` from `Build`, naming the scheme and its minimum |
| `pBestRate` outside (0, 1]; `archiveSizeRate` negative or not finite; `adaptationRate` outside [0, 1]; `memorySize < 1`; `maxEvaluationNumber < 1` | `ArgumentOutOfRangeException` |
| jDE's initial F not finite or ≤ 0, or initial CR outside [0, 1] | `ArgumentOutOfRangeException` |
| `maxStagnationStreak < 1`; `stagnationThreshold` negative or not finite | `ArgumentOutOfRangeException` |
| L-SHADE with an evaluation limit other than its `maxEvaluationNumber` | `InvalidOperationException` from `Build` |
| The archive's capacity·D above `int.MaxValue` | `InvalidOperationException` from `Build` |
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
doubles on the device and compiles two kernels; a configuration that needs bookkeeping
allocates its buffers and compiles its kernels too ([Bookkeeping](Bookkeeping/API.md)). A run copies the population to the host
once at the end, and once per observer call. No `GC.Collect`.

## Children

- [Objectives](Objectives/API.md) — the objective's contract and the gene view.
- [Devices](Devices/API.md) — device selection, ownership, the math probe (internal).
- [Kernels](Kernels/API.md) — the kernels and the DE step (internal).
- [Bookkeeping](Bookkeeping/API.md) — the work between generations (internal).
- [Random](Random/API.md) — Philox4x32-10 and the draw conversions (internal).

## Out of scope

- `float`.
- A caller's own scheme, variant, parameter provider (so the CPU package's dithered
  provider, reached only through its open interface), selection, stop rule, local
  search or initial sampling.
- An objective on the host or through `IFitnessFunctionEvaluator`: a `ReadOnlySpan`
  cannot cross into an ILGPU kernel.

## Audit fixes ⏳

Designed 2026-10-10 ([HISTORY.md](HISTORY.md#audit-fixes-decided-2026-10-10)), checks A1–A13
(Devices, Bookkeeping and Kernels `ACCEPTANCE.md`). No public signature changes.

- **The point type** (A5). `ForPointwiseFunction` throws `ArgumentException` (ParamName
  `TPoint`) unless `TPoint` has sequential layout, fields of primitive numeric types (not
  `bool`, not `char`), their enums or such structs, and no packing below its natural size.
- **Pointwise and monolithic** (P1 ⚠). The same arithmetic gives the same run bit for bit on
  the CPU accelerator; on a GPU the device compiler may fuse a multiply and an add that the
  pointwise form stores, so values can differ in the last bits.
- **`Dispose`** (A6–A8) releases everything even when a release throws, then throws an
  `AggregateException` of the failures; a second call, also a concurrent one, returns once
  the release is done. A `Build` that fails throws its own exception, with any release
  failures in `Data["DotNetDifferentialEvolution.GPU.ReleaseFailures"]`. After `Dispose` from
  the observer, release failures fault the task. Any exception on the run's thread faults
  the task.
- **New rows of the error table** (A5, A9): an unsupported `TPoint` → `ArgumentException`
  from `ForPointwiseFunction`; N or N·P above `int.MaxValue − 1 023` →
  `ArgumentOutOfRangeException` from `WithPopulationSize`; N·D or N·P above the limits when
  `Build` runs (a stage reused after `WithBounds`) → `InvalidOperationException` from
  `Build`; JADE, SHADE or L-SHADE with N above 2³⁰ → `InvalidOperationException` from `Build`.
- **Side effects** (A10, A13, DOC-2). `Build` compiles every kernel the configuration uses;
  `RunAsync` compiles none. A pointwise run allocates `N·P` point results and `2·N`
  doubles besides the population. With a stagnation limit the control block is copied
  without synchronising and read one interval later.
