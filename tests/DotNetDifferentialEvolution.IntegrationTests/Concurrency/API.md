# API.md — IntegrationTests/Concurrency

Nothing outward. What this node proves about the engine under real threads.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Cancelling from generation 5 stops after exactly 5 generations, with 1 and 4 workers; a pre-canceled token runs none; a canceled run disposes; an uncanceled token and no token change nothing | `CancellationTests` | ✅ |
| One seed reproduces the run bit for bit — best, genes, every fitness value — with 1 and 4 workers, the initial population and JADE's archive eviction included; different seeds differ; unseeded runs differ | `SeededReproducibilityTests` | ✅ |
| One worker, all cores, and twice the cores all converge on Sphere 6-D (1e-3); 25 repeated parallel runs all do | `ParallelDeterminismTests` | ✅ |
| 25 build/run/dispose cycles leave the global worker counter at its baseline and the thread count within `W + 8` | `WorkerLifecycleTests` | ✅ |
| 50 cycles grow the settled managed heap by at most 32 MB | `ResourceUsageTests` (`Slow`) | ✅ local only |

## Tests ✅

```csharp
[Trait("Category", "Integration")]
public class CancellationTests
{
    public async Task CancellingMidRunCompletesTheTaskAsCanceled(int workers);
    public async Task ATokenAlreadyCanceledStopsTheRunBeforeAnyGeneration();
    public async Task ACanceledRunDisposesWithoutHanging();
    public async Task AnUncanceledRunIsUnaffected();
    public async Task RunAsyncWithoutATokenStillWorks();
}
[Trait("Category", "Integration")]
public class SeededReproducibilityTests
{
    public async Task TheSameSeedReproducesTheRunExactly(int workers);
    public async Task TheSameSeedReproducesTheInitialPopulationToo(int workers);
    public async Task DifferentSeedsProduceDifferentRuns(int workers);
    public async Task AnUnseededRunIsStillFreeToDiffer();
    public async Task AnAdaptiveVariantIsReproducibleIncludingItsArchiveEviction(int workers);
}
[Trait("Category", "Integration")]
public class ParallelDeterminismTests
{
    public async Task SingleWorkerAndMultiWorkerBothConverge();
    public async Task RepeatedParallelRunsAllConvergeNoDataRaceCorruption();
    public async Task OversubscribedWorkerCountCompletesAndConverges();
}
[Trait("Category", "Integration")]
public class WorkerLifecycleTests
{
    public async Task RepeatedBuildRunDisposeDoesNotLeakWorkerControllers();
    public async Task RepeatedBuildRunDisposeDoesNotLeakThreads();
}
[Trait("Category", "Integration")]
[Trait("Category", "Slow")]
public class ResourceUsageTests
{
    public async Task RepeatedRunsDoNotGrowManagedHeapUnbounded();
}
```
