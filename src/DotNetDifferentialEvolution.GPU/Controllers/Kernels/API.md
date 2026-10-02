# API.md — GPU Kernels

Namespace: `DotNetDifferentialEvolution.GPU.Controllers.Kernels`. The kernel controller:
compiles the two kernels, owns the three device populations and runs the generation
loop. Everything not listed here is internal structure and may change.

## Controller ✅

```csharp
public sealed class KernelController<TFitnessFunctionInvoker, TRandomGenerator,
    TMutationStrategy, TSelectionStrategy> : IKernelController
    where TFitnessFunctionInvoker : struct, IFitnessFunctionInvoker
    where TRandomGenerator : struct, IRandomGenerator
    where TMutationStrategy : struct, IMutationStrategy<TRandomGenerator>
    where TSelectionStrategy : struct, ISelectionStrategy
{
    public KernelController(
        Context context,
        Accelerator device,
        IPopulationSamplingMaker populationSamplingMaker,
        TFitnessFunctionInvoker fitnessFunction,
        TRandomGenerator randomGenerator,
        TMutationStrategy mutationStrategy,
        TSelectionStrategy selectionStrategy,
        ITerminationStrategy terminationStrategy,
        IUpdateOptimizerHandler? updatedPopulationHandler = null);

    public void CompileAndGpuMemoryAlloc();
    public void Init();
    public void Run();
    public void Run(CancellationToken cancellationToken);
    public HostPopulation? GetCurrentPopulationOrNull();
    public void Dispose();
}
```

Implements [the contract](Interfaces/API.md). The constructor only stores its
arguments.

- `CompileAndGpuMemoryAlloc()` loads two auto-grouped kernels and allocates three
  populations — current, next, trial — each a fitness buffer and a `DenseX` gene
  buffer, all three filled with the **same** `TakeSamples()` genes and zero fitness.
- `Init()` launches the init kernel: the objective on every individual of the current
  population. Then synchronises.
- `Run(token)` reports `Starting` (generation 0) to the observer, then loops: launch the
  run kernel over `GetPopulationSize()` threads, synchronise, swap current and next,
  report `Running` with the generation number; until the termination rule returns
  `true` or the token is cancelled, checked in that order after each generation. Then
  reports `Terminating`. At least one generation always runs.
- The run kernel, per thread `index`: `Mutate(index, current, trial, random)`, then
  `Invoke(index, trial)`, then `Select(index, current, next, trial)`.
- `Dispose()` frees the three populations, then disposes `device` and `context`.

A second `Run` continues from the current population with the generation count
restarted at 0.

## Errors

| Situation | Behaviour |
|---|---|
| `CompileAndGpuMemoryAlloc` called twice | `InvalidOperationException` |
| `Init`/`Run` before allocation | `InvalidOperationException` |
| A strategy or objective struct ILGPU cannot compile | ILGPU's exception from `CompileAndGpuMemoryAlloc` |
| `Run` without `Init` | Not detected: the current fitness is still all zeros, so a trial survives only below 0 |
| Cancellation | No exception: `Run` returns after the generation in flight |

## Side effects

- Allocates and frees device memory for three populations.
- **Disposes the `Context` and `Accelerator` passed in**, which the caller created.
- Does not free the caller's other device buffers (bounds, random states).

## Out of scope

- Choosing the device, seeding, bounds: all come from the caller.
- Picking the best individual: the optimizer does it.
