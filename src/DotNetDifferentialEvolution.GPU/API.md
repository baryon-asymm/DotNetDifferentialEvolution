# API.md — DotNetDifferentialEvolution.GPU

Namespace: `DotNetDifferentialEvolution.GPU`. The GPU package's entry point and the map
of its parts. A caller assembles a run from struct strategies and an ILGPU device, hands
it to the optimizer and gets the best individual back. Everything not listed here is
internal structure and may change.

## How the package is used ✅

```csharp
public sealed class DifferentialEvolutionOptimizer
    : IDifferentialEvolutionOptimizer<OptimizationResult>, IDisposable
{
    public DifferentialEvolutionOptimizer(IKernelController kernelController);

    public Task<OptimizationResult> RunAsync();
    public Task<OptimizationResult> RunAsync(CancellationToken cancellationToken);
    public void Dispose();
}
```

The path through the nodes:

1. The caller creates an ILGPU `Context` and `Accelerator`, allocates device buffers
   for the box bounds and for the random states (one `XorShift32` per individual,
   non-zero seeds), and writes the objective as a struct implementing
   [`IFitnessFunctionInvoker`](Interfaces/API.md).
2. It builds the parts: [`PopulationSamplingMaker`](PopulationSamplingMakers/API.md)
   (size and box, upper bound first), [`RandomGenerator`](RandomGenerators/API.md),
   [`MutationStrategy<RandomGenerator>`](MutationStrategies/API.md) (lower bound
   first), [`SelectionStrategy`](SelectionStrategies/API.md) and
   [`MaxGenerationStrategy`](TerminationStrategies/API.md).
3. It passes them, with the context and the device, to
   [`KernelController<…>`](Controllers/Kernels/API.md), and the controller to this
   optimizer.
4. The **constructor** compiles the kernels and allocates the populations
   (`CompileAndGpuMemoryAlloc`); ILGPU compilation errors surface here.
5. `RunAsync` evaluates the current population, runs generations until the stop rule
   or the token, copies the whole current population to the host and returns the
   individual with the lowest fitness (the first one found on a tie) as an
   [`OptimizationResult`](Models/API.md). It does all this **synchronously** on the
   calling thread and returns a completed task.
6. `Dispose` disposes the controller — which disposes the context and the device —
   and then calls `GC.Collect()`.

A second `RunAsync` continues from where the first stopped: it re-evaluates the current
population and runs a new full count of generations.

## Errors

| Situation | Behaviour |
|---|---|
| The kernels cannot be compiled | ILGPU's exception from the constructor |
| No current population when picking the result | `InvalidOperationException` (cannot happen after a successful constructor) |
| Cancellation | A normal result from the population reached so far; no `OperationCanceledException` |
| Individual 0 has fitness `NaN` | Returned as the best: every comparison with `NaN` is false (by reading the code) |

## Side effects

Blocks the calling thread for the whole run; copies the population to the host once
per `RunAsync`; forces a full garbage collection on `Dispose`.

## Children

- [Interfaces](Interfaces/API.md) — the optimizer, objective and observer contracts.
- [Models](Models/API.md) — device and host populations, result types.
- [Controllers/Kernels](Controllers/Kernels/API.md) — kernel controller and its
  [contract](Controllers/Kernels/Interfaces/API.md).
- [MutationStrategies](MutationStrategies/API.md) — DE/rand/1/bin, and its
  [contract](MutationStrategies/Interfaces/API.md).
- [SelectionStrategies](SelectionStrategies/API.md) — greedy selection, and its
  [contract](SelectionStrategies/Interfaces/API.md).
- [RandomGenerators](RandomGenerators/API.md) — per-thread `XorShift32`, and its
  [contract](RandomGenerators/Interfaces/API.md).
- [PopulationSamplingMakers](PopulationSamplingMakers/API.md) — uniform box sampler,
  and its [contract](PopulationSamplingMakers/Interfaces/API.md).
- [TerminationStrategies](TerminationStrategies/API.md) — generation limit, and its
  [contract](TerminationStrategies/Interfaces/API.md).

## Out of scope

- A builder: every part is assembled by hand.
- Seeding a run, adaptive variants (jDE, JADE, SHADE, L-SHADE), other mutation schemes,
  stop rules other than a generation count.
- `DotNetOptimization.Abstractions`: the result is not an `ISolution` and the objective
  is not an `IFitnessFunctionEvaluator`.

## v1 contract ⏳

Designed on 2026-10-03; nothing below exists yet (`BOOT.md`, `## v1 design ⏳`). It
replaces everything above at 1.0.0. The objective's contract is in the
`DotNetDifferentialEvolution.GPU.Objectives` namespace; everything else is in
`DotNetDifferentialEvolution.GPU`.

### Objective

```csharp
public interface IGpuFitnessFunction
{
    double Evaluate(GeneView genes);
}

public readonly struct GeneView
{
    public int Length { get; }
    public double this[int index] { get; }
}
```

- **Shape.** A struct implementation, compiled into the kernel. `Evaluate` is called
  once per individual per launch, one GPU thread each.
- **Input and output.** `genes` is that individual's `D` genes and nothing else, and it
  cannot be written through. The return value is the fitness; lower is better, and
  `NaN` is allowed (it ranks worst).
- **Data.** Any data the objective needs (fit points, constants) it carries as fields:
  value types and ILGPU `ArrayView`s the caller allocated on the same accelerator.
- **No bounds check.** Reading `genes[j]` outside `0 ≤ j < Length` is undefined, because
  kernels cannot throw.
- **What the body may use.** The kernel rules of `BOOT.md`, `## Constraints`, plus the
  `Math` allow-list of invariant 8.

### Builder

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

public enum GpuDevice { Auto = 0, Cuda, OpenCL, Cpu }
```

- **DE/rand/1/bin is the only scheme**, named as in the CPU builder.
- **The limits.** A generation limit runs exactly that many generations (≥ 1). An
  evaluation limit stops at the first generation boundary where the count, starting at
  N, is ≥ the limit; this is the CPU package's rule.
- **Devices.**
  - `OnDevice(Auto)` tries CUDA, then OpenCL, then the CPU accelerator.
  - An explicit `Cuda`, `OpenCL` or `Cpu` uses that device or makes `Build` throw.
  - `OnAccelerator` uses the caller's accelerator and never disposes it.
- **`Build`** samples the population on the device, compiles the kernels and evaluates
  the initial population, so it costs N evaluations and the compile time. Kernel
  compile errors surface here.
- **Unseeded runs.** Without `WithSeed`, the seed is one draw from `Random.Shared`.

### Optimizer and result

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

- **`RunAsync`** runs the generations on a dedicated thread bound to the accelerator,
  and returns at once.
  - The result is the best individual of the final population: `NaN` is worst, a tie
    goes to the lowest index.
  - The token is observed between generations and ends the task as canceled.
  - After the run, a second call returns the same task; during the run it throws.
- **`Device.FallbackReason`** says why `Auto` skipped a backend. It is `null` when
  nothing was skipped or the device was explicit.
- **The observer** gets a host copy of the population every `everyNGenerations`
  generations, on the run thread. That copy is the only per-generation transfer, and the
  caller opts into it.

### Errors of v1

| Situation | Behaviour |
|---|---|
| Bounds of different lengths, empty, or lower > upper | `ArgumentException` from `WithBounds` |
| `populationSize < 4` (rand/1 needs four distinct individuals) | `ArgumentOutOfRangeException` |
| `mutationForce` not finite or ≤ 0; `crossoverProbability` outside [0, 1] | `ArgumentOutOfRangeException` |
| A limit < 1 | `ArgumentOutOfRangeException` |
| `everyNGenerations < 1` | `ArgumentOutOfRangeException` |
| `null` handler or accelerator | `ArgumentNullException` |
| An explicit device that is not present | `InvalidOperationException` from `Build`, naming the device |
| The objective cannot be compiled by ILGPU | ILGPU's exception from `Build` |
| `RunAsync` while a run is in progress | `InvalidOperationException` |
| The observer throws | the task faults with that exception |

### Out of scope for v1

- `float`, jDE and the other adaptive variants, schemes other than DE/rand/1/bin.
- Stop rules other than the two limits, and a public termination interface.
- An objective on the host or through `IFitnessFunctionEvaluator`: a `ReadOnlySpan`
  cannot cross into an ILGPU kernel.
