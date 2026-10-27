using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// Integration tests for multi-worker orchestration. These runs are genuinely parallel (one
/// worker per processor) and unseeded (the engine gives each worker its own seeded provider,
/// but the harness passes no seed); convergence is asserted on tolerance rather than exact
/// reproduction. Every wait is bounded by a timeout.
/// </summary>
[Trait("Category", "Integration")]
public class WorkersOrchestratorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly int WorkersCount = Math.Max(2, Environment.ProcessorCount);

    /// <summary>
    /// One worker per processor, sharing one population, together reach the 2-D Rosenbrock optimum.
    /// Once the result is in, no slave is running (asserted at once, as the handler promises); the
    /// master, which carries the handler, leaves its loop within a deadline.
    /// </summary>
    [Fact]
    public async Task AllWorkersCooperateToConverge()
    {
        var evaluator = new RosenbrockEvaluator(dimension: 2);
        var termination = new StagnationStreakTerminationStrategy(maxStagnationStreak: 2000, stagnationThreshold: 1e-9);
        var (context, executor) = ExecutorFactory.Create(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9,
            populationSize: 300, workersCount: WorkersCount, seed: null);

        using var harness = new MultiWorkerHarness(context, executor, WorkersCount);

        harness.StartAll();
        var result = await harness.Handler.GetResultPopulationTask().WaitAsync(Timeout).ConfigureAwait(true);

        Assert.False(harness.AnySlaveRunning);
        ConvergenceAssert.ReachedOptimum(evaluator, result, valueTolerance: 1e-5, geneTolerance: 1e-2);
        await WorkerStopAssert.LeavesItsLoopAsync(harness.Master).ConfigureAwait(true);
    }

    /// <summary>
    /// A fitness-function exception thrown in any worker fails the result task with one to
    /// one-per-worker inner exceptions, all of the thrown type. Once the failure is in, no slave is
    /// running (asserted at once); the master leaves its loop within a deadline.
    /// </summary>
    [Fact]
    public async Task FitnessFunctionExceptionPropagatesFromAnyWorkerAndStopsAll()
    {
        const int populationSize = 300;
        var evaluator = new ExceptionRosenbrockEvaluator(throwExceptionAt: 2 * populationSize + 1);
        var termination = new LimitGenerationNumberTerminationStrategy(100_000);
        var (context, executor) = ExecutorFactory.Create(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9,
            populationSize: populationSize, workersCount: WorkersCount, seed: null);

        using var harness = new MultiWorkerHarness(context, executor, WorkersCount);

        harness.StartAll();

        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => harness.Handler.GetResultPopulationTask().WaitAsync(Timeout)).ConfigureAwait(true);

        Assert.False(harness.AnySlaveRunning);
        Assert.NotEmpty(aggregate.InnerExceptions);
        Assert.InRange(aggregate.InnerExceptions.Count, 1, WorkersCount);
        Assert.All(aggregate.InnerExceptions, ex => Assert.IsType<RosenbrockException>(ex));
        await WorkerStopAssert.LeavesItsLoopAsync(harness.Master).ConfigureAwait(true);
    }
}
