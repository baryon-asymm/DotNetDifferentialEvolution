# API.md — GPU.Test/EndToEnd

Nothing outward. What this node proves about whole runs of the GPU package: convergence,
reproducibility, host transfers, asynchrony and cancellation, ownership of a caller's
accelerator, and the run errors of the v1 contract.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| 1h: seed 1, N = 50, F = 0.5, CR = 0.9, 1000 generations: Sphere 5-D reaches 1e-6; Rosenbrock 2-D reaches 1e-6 with genes within 1e-3 of (1, 1); Rastrigin 2-D reaches 1e-4 | `ConvergenceTests`, CPU accelerator; CUDA and OpenCL under `Gpu` | ✅ |
| The series cosine of Rastrigin matches `Math.Cos` to 1e-13 and is exactly 1 at integers | `TheRastriginCosineMatchesSystemMath` | ✅ |
| 4a: the same seed twice gives a bit-identical final population and result; seeds 1 and 2 differ | `ReproducibilityTests`, CPU accelerator; CUDA and OpenCL under `Gpu` | ✅ |
| 5b: 100 generations with no observer download the population once; with an observer every 10 generations, 11 times | `TransferCountTests` | ✅ |
| 6a: while the observer is held at generation 1, `RunAsync` has returned an incomplete task | `RunAsyncReturnsAnIncompleteTaskWhileTheObserverIsHeld` | ✅ |
| 6b: a token cancelled from the observer at generation 3 ends the task as canceled after at most 4 generations | `ATokenCancelledAtGenerationThreeEndsTheTaskAsCanceled` | ✅ |
| 6c: after the run a second `RunAsync` returns the same task; during it, `InvalidOperationException` (also B1's row) | `ASecondCallAfterTheRunReturnsTheSameTask`, `ACallDuringTheRunThrows` | ✅ |
| 7b: a caller-owned accelerator still allocates, runs a kernel into and reads back a buffer after the optimizer's `Dispose` | `OwnershipTests`, CPU accelerator; CUDA and OpenCL under `Gpu` | ✅ |
| B1: an observer that throws faults the task with that same exception | `AThrowingObserverFaultsTheTaskWithItsException` | ✅ |
| S13: seed 1, Sphere 10-D, 2·10⁵ evaluations: each of the nine configurations reaches 1e-6 on the CPU accelerator and on CUDA, and the CPU package with the same configuration does too | `SymmetryRunTests.EachConfigurationConverges…`; CUDA under `Gpu` | ✅ |
| S14: each configuration and the stagnation rule, twice with one seed: bit-identical results and snapshots | `SymmetryRunTests.EachConfigurationIsReproducible…`, `TheStagnationLimitIsReproducible`; CUDA under `Gpu` | ✅ |
| S17: with a stagnation limit the stop word is read at most ⌈G/16⌉ + observer calls + 1 times; without one, never | `TheStopWordIsReadOnlyEverySixteenGenerationsAndForTheObserver` | ✅ |
| S18: a limit or a cancellation after the stop, before its read, ends the run at the stopping generation; a cancellation before it cancels | `StopWordExitTests` | ✅ |
| S19: SHADE and L-SHADE on an objective scoring infeasible points `double.MaxValue` and a `NaN` gene 0 (8-D sphere feasible on `x₀ < −4`, N = 100, 20 000 evaluations, seed 12345) end with no `NaN` gene in the best individual and a finite fitness; the same for L-SHADE on CUDA (N = 16 384, 32 genes, 5·10⁶ evaluations, seed 20261007) | `SentinelFitnessTests`; CUDA under `Gpu` | ⏳ |
| The package README's quick start compiles and, on whatever device Auto finds, reaches Sphere's minimum below 1e-12 | `DocumentedExampleTests` | ✅ |

## Tests ✅

```csharp
public class DocumentedExampleTests
{
    public Task TheQuickStartBuildsRunsAndReachesTheMinimum();
}
public class ConvergenceTests
{
    public ConvergenceTests(ITestOutputHelper output);
    public Task SphereReachesItsMinimumOnTheCpuAccelerator();
    public Task RosenbrockReachesItsMinimumOnTheCpuAccelerator();
    public Task RastriginReachesItsMinimumOnTheCpuAccelerator();
    [Trait("Category", "Gpu")]
    public Task SphereReachesItsMinimumOnTheGpu(GpuDevice device);
    [Trait("Category", "Gpu")]
    public Task RosenbrockReachesItsMinimumOnTheGpu(GpuDevice device);
    [Trait("Category", "Gpu")]
    public Task RastriginReachesItsMinimumOnTheGpu(GpuDevice device);
}
public class ObjectiveTests
{
    public void TheRastriginCosineMatchesSystemMath();
}
public class ReproducibilityTests
{
    public Task TheSameSeedTwiceIsBitIdenticalOnTheCpuAccelerator();
    public Task SeedsOneAndTwoDifferOnTheCpuAccelerator();
    [Trait("Category", "Gpu")]
    public Task TheSameSeedTwiceIsBitIdenticalOnTheGpu(GpuDevice device);
    [Trait("Category", "Gpu")]
    public Task SeedsOneAndTwoDifferOnTheGpu(GpuDevice device);
}
public class TransferCountTests
{
    public async Task ARunWithoutAnObserverDownloadsThePopulationOnce();
    public async Task ARunWithAnObserverEveryTenGenerationsDownloadsElevenTimes();
}
public class AsynchronyTests
{
    public async Task RunAsyncReturnsAnIncompleteTaskWhileTheObserverIsHeld();
    public async Task ATokenCancelledAtGenerationThreeEndsTheTaskAsCanceled();
    public async Task ASecondCallAfterTheRunReturnsTheSameTask();
    public async Task ACallDuringTheRunThrows();
}
public class OwnershipTests
{
    public async Task ACallerOwnedCpuAcceleratorOutlivesTheOptimizer();
    [Trait("Category", "Gpu")]
    public async Task ACallerOwnedGpuAcceleratorOutlivesTheOptimizer(GpuDevice device);
}
public class RunErrorTests
{
    public async Task AThrowingObserverFaultsTheTaskWithItsException();
}
public class StopWordExitTests
{
    [Trait("Category", "Integration")]
    public async Task ALimitAfterTheStopEndsTheRunAtTheStoppingGeneration();
    [Trait("Category", "Integration")]
    public async Task ACancellationAfterTheStopCompletesTheRunAtTheStoppingGeneration();
    [Trait("Category", "Integration")]
    public async Task ACancellationBeforeTheStopStillCancels();
}
public class SentinelFitnessTests
{
    public SentinelFitnessTests(ITestOutputHelper output);
    [Trait("Category", "Integration")]
    public Task ShadeEndsWithNoNaNGeneWhenInfeasiblePointsScoreMaxValue();
    [Trait("Category", "Integration")]
    public Task LShadeEndsWithNoNaNGeneWhenInfeasiblePointsScoreMaxValue();
    [Trait("Category", "Gpu")]
    public Task LShadeEndsWithNoNaNGeneWhenInfeasiblePointsScoreMaxValueOnCuda();
}
public class SymmetryRunTests
{
    public SymmetryRunTests(ITestOutputHelper output);
    public static TheoryData<string> Configurations();
    [Trait("Category", "Integration")]
    public Task EachConfigurationConvergesOnTheCpuAccelerator(string configuration);
    [Trait("Category", "Gpu")]
    public Task EachConfigurationConvergesOnCuda(string configuration);
    [Trait("Category", "Integration")]
    public Task EachConfigurationIsReproducibleOnTheCpuAccelerator(string configuration);
    [Trait("Category", "Gpu")]
    public Task EachConfigurationIsReproducibleOnCuda(string configuration);
    [Trait("Category", "Integration")]
    public async Task TheStagnationLimitIsReproducible();
    [Trait("Category", "Integration")]
    public async Task TheStopWordIsReadOnlyEverySixteenGenerationsAndForTheObserver();
}
```

Internal helpers: the objectives `Sphere`, `Rosenbrock`, `Rastrigin`; the observers
`RecordingObserver`, `GateObserver`, `CancellingObserver`, `ThrowingObserver`; and
`HangGuard`, the bound on every wait.
