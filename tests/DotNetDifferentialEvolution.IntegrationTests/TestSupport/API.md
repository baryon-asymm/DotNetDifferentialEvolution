# API.md — IntegrationTests/TestSupport

Namespace: `DotNetDifferentialEvolution.IntegrationTests.TestSupport`. Internal to the
test project.

## Runners and assertion ✅

```csharp
internal static class ManualAlgorithmRunner
{
    public static Population Run(ITestFitnessFunctionEvaluator evaluator,
        ITerminationStrategy terminationStrategy, double mutationForce,
        double crossoverProbability, int populationSize, int seed);
}

internal static class ExecutorFactory
{
    public static (ProblemContext Context, AlgorithmExecutor Executor) Create(
        ITestFitnessFunctionEvaluator evaluator, ITerminationStrategy terminationStrategy,
        double mutationForce, double crossoverProbability, int populationSize,
        int workersCount, int? seed, IGenerationStrategy? generationStrategy = null);
}

internal sealed class MultiWorkerHarness : IDisposable
{
    public MultiWorkerHarness(ProblemContext context, AlgorithmExecutor executor, int workersCount);
    public OrchestratorWorkerHandler Handler { get; }
    public WorkerController Master { get; }
    public void StartAll();
    public bool AnySlaveRunning { get; }
    public void Dispose();
}

internal static class WorkerStopAssert
{
    public static readonly TimeSpan Deadline;   // 10 s
    public static Task LeavesItsLoopAsync(WorkerController worker);
}

internal static class BuilderOptimizer
{
    public const int Seed = 1;
    public static async Task<Population> RunOnceAsync(TimeSpan timeout,
        Func<DifferentialEvolution> factory);
}

internal static class ConvergenceAssert
{
    public static void ReachedOptimum(ITestFitnessFunctionEvaluator evaluator,
        Population population, double valueTolerance, double? geneTolerance = null);
}
```

- `ManualAlgorithmRunner` drives `Execute → SwapPopulations → GetRepresentativePopulation`
  on the calling thread until the stop rule fires; seeded, so reproducible.
- `ExecutorFactory` builds a context through `ProblemContextHelper` and a classic
  executor (legacy `MutationStrategy`, greedy selection). The seed is used only when
  `workersCount == 1`.
- `MultiWorkerHarness` wires `W − 1` slaves and a master around one orchestrator handler
  and starts them in the production order: slaves first, the master last
  (`DifferentialEvolution.RunAsync`). `Master` is the worker carrying the handler;
  `AnySlaveRunning` is `true` while any slave's `IsRunning` is. There is no
  "any worker running" member: the only thing it could be asserted against at once is not
  promised (the parent's invariant "After the result", [BOOT.md](../BOOT.md)).
- `BuilderOptimizer.RunOnceAsync` builds and runs once and moves the cursor to the best
  individual. Callers seed the builder with `BuilderOptimizer.Seed`, so each convergence
  test is one reproducible run.
- `WorkerStopAssert.LeavesItsLoopAsync` polls `worker.IsRunning` (`await Task.Delay(1)`
  between reads, a `Stopwatch` against `Deadline`) and returns as soon as it reads
  `false`; at the deadline it fails with `Assert.False(worker.IsRunning, message)`, the
  message naming the worker id and the deadline. It never calls `Stop` or `Dispose`:
  the worker must leave by itself. Ten seconds is a hang bound, not a measurement: the
  worker has only to return from `Handle`.
- `ConvergenceAssert.ReachedOptimum` compares the cursor's value (two-sided) and,
  optionally, each gene with the evaluator's declared optimum.
