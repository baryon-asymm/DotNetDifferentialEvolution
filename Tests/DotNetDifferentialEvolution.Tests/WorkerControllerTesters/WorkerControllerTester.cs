using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.Tests.WorkerControllerTesters;

public class WorkerControllerTester
{
    private readonly ITestOutputHelper _testOutputHelper;

    public WorkerControllerTester(
        ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestBaseCase()
    {
#region Setup

        const int populationSize = 300;
        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;
        var evaluator = new RosenbrockEvaluator();

        const int maxGenerationNumber = 100_000;
        var terminationStrategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber);
        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);

        var orchestratorWorkerHandler = new OrchestratorWorkerHandler(
            Memory<WorkerController>.Empty,
            context);

        const int workerId = 0;
        var workerController = new WorkerController(workerId, algorithmExecutor, orchestratorWorkerHandler);

#endregion

#region Execution

        workerController.Start();

        var resultPopulation = await orchestratorWorkerHandler.GetResultPopulationTask();
        resultPopulation.MoveCursorToBestIndividual();

#endregion

#region Validation

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes();
        Assert.Equal(resultPopulation.IndividualCursor.FitnessFunctionValue, globalMinimumFfValue, tolerance);
        var genes = resultPopulation.IndividualCursor.Genes;
        for (int i = 0; i < resultPopulation.GenomeSize; i++)
            Assert.Equal(genes.Span[i], globalMinimumGenes.Span[i], tolerance);

#endregion
    }

    [Fact]
    public async Task TestWithException()
    {
#region Setup

        const int populationSize = 300;
        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;
        const int throwExceptionAtEvaluationStep = 2 * populationSize;
        var evaluator = new ExceptionRosenbrockEvaluator(throwExceptionAtEvaluationStep);

        const int maxGenerationNumber = 100_000;
        var terminationStrategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber);
        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);

        var orchestratorWorkerHandler = new OrchestratorWorkerHandler(
            Memory<WorkerController>.Empty,
            context);

        const int workerId = 0;
        var workerController = new WorkerController(workerId, algorithmExecutor, orchestratorWorkerHandler);

#endregion

#region Execution

        workerController.Start();

#endregion

#region Validation

        var aggregateException = await Assert.ThrowsAsync<AggregateException>(
                                     () => orchestratorWorkerHandler.GetResultPopulationTask());
        Assert.Single(aggregateException.InnerExceptions);
        Assert.IsType<RosenbrockException>(aggregateException.InnerExceptions.First());

#endregion
    }

    // Long-running test (a one minute)
    [Fact]
    public async Task TestWithRandomStopAndStart()
    {
#region Setup
        
        const double totalTestTimeSeconds = 60;

        const int populationSize = 300;
        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;
        var evaluator = new RosenbrockEvaluator();

        var terminationStrategy = new TerminationStrategySwitcher();
        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);

        var orchestratorWorkerHandler = new OrchestratorWorkerHandler(
            Memory<WorkerController>.Empty,
            context);

        const int workerId = 0;
        using var workerController = new WorkerController(workerId, algorithmExecutor, orchestratorWorkerHandler);

        var random = Random.Shared;
        var currentState = GetTreeOfStates();

#endregion

#region Execution
        
        var testTimeWithRandom = TimeSpan.FromSeconds(totalTestTimeSeconds / 2);
        var testTimeForTermination = TimeSpan.FromSeconds(totalTestTimeSeconds / 2);
        
        var initialTime = DateTime.Now;
        while (DateTime.Now - initialTime < testTimeWithRandom)
        {
            var command = random.Next(2);
            var nextState = currentState.NextStates[command];
            switch (command)
            {
                case (int)WorkerCommands.Start:
                    if (nextState.MustThrowException)
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                        {
                            await Task.Yield();
                            workerController.Start(throwIfRunning: true);
                        });
                    }
                    else
                    {
                        workerController.Start(throwIfRunning: true);
                    }

                    Assert.False(workerController.IsRunning ^ nextState.IsRunning); // Xor, must be same
                    break;

                case (int)WorkerCommands.Stop:
                    if (nextState.MustThrowException)
                    {
                        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                        {
                            await Task.Yield();
                            workerController.Stop(throwIfStopped: true);
                        });
                    }
                    else
                    {
                        workerController.Stop(throwIfStopped: true);
                    }

                    Assert.False(workerController.IsRunning ^ nextState.IsRunning); // Xor, must be same
                    break;
            }

            currentState = nextState;
        }

        if (workerController.IsRunning)
            workerController.Stop(throwIfStopped: true);

        workerController.Start(throwIfRunning: true);
        
        await Task.Delay(testTimeForTermination);
        terminationStrategy.SetShouldTerminate(shouldTerminate: true);
        
        if (workerController.IsRunning)
            workerController.Stop(throwIfStopped: true);

        var resultPopulation = await orchestratorWorkerHandler.GetResultPopulationTask();
        resultPopulation.MoveCursorToBestIndividual();

#endregion

#region Validation

        Assert.False(workerController.IsRunning);

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes();
        Assert.Equal(resultPopulation.IndividualCursor.FitnessFunctionValue, globalMinimumFfValue, tolerance);
        var genes = resultPopulation.IndividualCursor.Genes;
        for (int i = 0; i < resultPopulation.GenomeSize; i++)
            Assert.Equal(genes.Span[i], globalMinimumGenes.Span[i], tolerance);

#endregion
    }

    private static ProblemContext CreateContext(
        int populationSize,
        double mutationForce,
        double crossoverProbability,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        out IAlgorithmExecutor algorithmExecutor)
    {
        var context = ProblemContextHelper.CreateContext(
            populationSize, testFitnessFunctionEvaluator, terminationStrategy);
        var randomProvider = new RandomProvider();
        var mutationStrategy = new MutationStrategy(mutationForce, crossoverProbability, randomProvider, context);
        var selectionStrategy = new SelectionStrategy(context);
        algorithmExecutor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);

        return context;
    }

#region Utils for TestWithRandomStopAndStart

    private class TerminationStrategySwitcher : ITerminationStrategy
    {
        private volatile bool _shouldTerminate;

        public void SetShouldTerminate(
            bool shouldTerminate)
        {
            _shouldTerminate = shouldTerminate;
        }

        public bool ShouldTerminate(
            Population population)
        {
            return _shouldTerminate;
        }
    }

    private enum WorkerCommands : int
    {
        Start,
        Stop
    }

    private class WorkerExpectationState
    {
        public bool MustThrowException { get; init; }

        public bool IsRunning { get; init; }

        public WorkerExpectationState[] NextStates { get; init; }
    }

    private static WorkerExpectationState GetTreeOfStates()
    {
        var initialState = new WorkerExpectationState()
        {
            IsRunning = false,
            MustThrowException = false,
            NextStates = new WorkerExpectationState[2]
        };

        var initialToStart = new WorkerExpectationState()
        {
            IsRunning = true,
            MustThrowException = false,
            NextStates = new WorkerExpectationState[2]
        };

        initialState.NextStates[(int)WorkerCommands.Start] = initialToStart;

        var runningToStart = new WorkerExpectationState()
        {
            IsRunning = true,
            MustThrowException = true,
            NextStates = new WorkerExpectationState[2]
        };

        runningToStart.NextStates[(int)WorkerCommands.Start] = runningToStart;
        runningToStart.NextStates[(int)WorkerCommands.Stop] = initialState;

        var stoppedToStop = new WorkerExpectationState()
        {
            IsRunning = false,
            MustThrowException = true,
            NextStates = new WorkerExpectationState[2]
        };

        stoppedToStop.NextStates[(int)WorkerCommands.Start] = initialToStart;
        stoppedToStop.NextStates[(int)WorkerCommands.Stop] = stoppedToStop;

        initialToStart.NextStates[(int)WorkerCommands.Start] = runningToStart;
        initialToStart.NextStates[(int)WorkerCommands.Stop] = initialState;

        initialState.NextStates[(int)WorkerCommands.Stop] = stoppedToStop;

        return initialState;
    }

#endregion
}
