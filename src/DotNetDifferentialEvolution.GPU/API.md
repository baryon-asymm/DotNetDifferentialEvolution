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
