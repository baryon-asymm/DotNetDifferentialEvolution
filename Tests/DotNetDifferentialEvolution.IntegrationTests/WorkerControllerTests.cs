using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;
using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// Integration tests for a single <see cref="WorkerController"/> driven by an
/// <see cref="OrchestratorWorkerHandler"/>. Every wait is bounded by a timeout so a hang
/// fails fast instead of stalling the suite (guarding the fixed TaskCompletionSource deadlock).
/// </summary>
[Trait("Category", "Integration")]
public class WorkerControllerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ConvergesAndStopsOnTermination()
    {
        var evaluator = new RosenbrockEvaluator(dimension: 2);
        var termination = new StagnationStreakTerminationStrategy(maxStagnationStreak: 2000, stagnationThreshold: 1e-9);
        var (context, executor) = ExecutorFactory.Create(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9,
            populationSize: 100, workersCount: 1, seed: 4242);

        var handler = new OrchestratorWorkerHandler(Memory<WorkerController>.Empty, context);
        using var worker = new WorkerController(workerId: 0, executor, handler);

        worker.Start();
        var result = await handler.GetResultPopulationTask().WaitAsync(Timeout);

        ConvergenceAssert.ReachedOptimum(evaluator, result, valueTolerance: 1e-6, geneTolerance: 1e-3);
        Assert.False(worker.IsRunning);
    }

    [Fact]
    public async Task PropagatesFitnessFunctionExceptionAndStops()
    {
        const int populationSize = 200;
        var evaluator = new ExceptionRosenbrockEvaluator(throwExceptionAt: 2 * populationSize);
        var termination = new LimitGenerationNumberTerminationStrategy(100_000);
        var (context, executor) = ExecutorFactory.Create(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9,
            populationSize: populationSize, workersCount: 1, seed: 1);

        var handler = new OrchestratorWorkerHandler(Memory<WorkerController>.Empty, context);
        using var worker = new WorkerController(workerId: 0, executor, handler);

        worker.Start();

        var aggregate = await Assert.ThrowsAsync<AggregateException>(
            () => handler.GetResultPopulationTask().WaitAsync(Timeout));
        Assert.Single(aggregate.InnerExceptions);
        Assert.IsType<RosenbrockException>(aggregate.InnerExceptions[0]);
        Assert.False(worker.IsRunning);
    }

    // Bounded, deterministic replacement for the old 60-second wall-clock stop/start test:
    // a fixed number of random (but seeded) start/stop commands walked against an expected
    // state machine, then a clean termination. Slow because it still exercises real threads.
    [Fact]
    [Trait("Category", "Slow")]
    public async Task RandomStopAndStartKeepsConsistentStateThenTerminates()
    {
        const int commandCount = 2000;

        var evaluator = new RosenbrockEvaluator(dimension: 2);
        var termination = new TerminationStrategySwitcher();
        var (context, executor) = ExecutorFactory.Create(
            evaluator, termination, mutationForce: 0.5, crossoverProbability: 0.9,
            populationSize: 200, workersCount: 1, seed: null);

        var handler = new OrchestratorWorkerHandler(Memory<WorkerController>.Empty, context);
        using var worker = new WorkerController(workerId: 0, executor, handler);

        var random = new Random(20240601);
        var state = BuildStateMachine();

        for (int i = 0; i < commandCount; i++)
        {
            var command = (WorkerCommand)random.Next(2);
            var next = state.Next[(int)command];

            switch (command)
            {
                case WorkerCommand.Start:
                    if (next.MustThrow)
                        Assert.Throws<InvalidOperationException>(() => worker.Start(throwIfRunning: true));
                    else
                        worker.Start(throwIfRunning: true);
                    break;
                case WorkerCommand.Stop:
                    if (next.MustThrow)
                        Assert.Throws<InvalidOperationException>(() => worker.Stop(throwIfStopped: true));
                    else
                        worker.Stop(throwIfStopped: true);
                    break;
            }

            Assert.Equal(next.IsRunning, worker.IsRunning);
            state = next;
        }

        if (worker.IsRunning == false)
            worker.Start(throwIfRunning: true);

        termination.SetShouldTerminate(true);
        var result = await handler.GetResultPopulationTask().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.NotNull(result);
        Assert.False(worker.IsRunning);
    }

    private sealed class TerminationStrategySwitcher : ITerminationStrategy
    {
        private volatile bool _shouldTerminate;

        public void SetShouldTerminate(bool shouldTerminate) => _shouldTerminate = shouldTerminate;

        public bool ShouldTerminate(Population population) => _shouldTerminate;
    }

    private enum WorkerCommand
    {
        Start = 0,
        Stop = 1,
    }

    private sealed class ExpectedState
    {
        public bool MustThrow { get; init; }
        public bool IsRunning { get; init; }
        public ExpectedState[] Next { get; init; } = new ExpectedState[2];
    }

    // Mirrors the worker's start/stop contract: starting a running worker (or stopping a
    // stopped one) with throwIfRunning/throwIfStopped must throw and leave the state unchanged.
    private static ExpectedState BuildStateMachine()
    {
        var stopped = new ExpectedState { IsRunning = false, MustThrow = false };
        var running = new ExpectedState { IsRunning = true, MustThrow = false };
        var runningThrow = new ExpectedState { IsRunning = true, MustThrow = true };
        var stoppedThrow = new ExpectedState { IsRunning = false, MustThrow = true };

        stopped.Next[(int)WorkerCommand.Start] = running;
        stopped.Next[(int)WorkerCommand.Stop] = stoppedThrow;

        running.Next[(int)WorkerCommand.Start] = runningThrow;
        running.Next[(int)WorkerCommand.Stop] = stopped;

        runningThrow.Next[(int)WorkerCommand.Start] = runningThrow;
        runningThrow.Next[(int)WorkerCommand.Stop] = stopped;

        stoppedThrow.Next[(int)WorkerCommand.Start] = running;
        stoppedThrow.Next[(int)WorkerCommand.Stop] = stoppedThrow;

        return stopped;
    }
}
