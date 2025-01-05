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
using DotNetDifferentialEvolution.Tests.WorkerControllerTesters;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.Tests.WorkersOrchestratorTesters;

public class WorkersOrchestratorTester
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public WorkersOrchestratorTester(
        ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    [Fact]
    public async Task TestBaseCase()
    {
#region Setup

        int workersCount = Environment.ProcessorCount;
        
        const int populationSize = 300;
        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;
        var evaluator = new RosenbrockEvaluator();

        const int maxGenerationNumber = 100_000;
        var terminationStrategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber);
        var context = CreateContext(workersCount,
                                    populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);
        
        var slaveWorkersCount = workersCount - 1;
        var slaveWorkers = new WorkerController[slaveWorkersCount];
        for (int i = 0; i < slaveWorkersCount; i++)
            slaveWorkers[i] = new WorkerController(workerId: i, algorithmExecutor);

        var orchestratorWorkerHandler = new OrchestratorWorkerHandler(
            slaveWorkers.ToArray(),
            context);

        var masterWorkerId = workersCount - 1;
        using var masterWorker = new WorkerController(masterWorkerId, algorithmExecutor, orchestratorWorkerHandler);

#endregion

#region Execution

        masterWorker.Start();
        foreach (var slaveWorker in slaveWorkers)
            slaveWorker.Start();

        var resultPopulation = await orchestratorWorkerHandler.GetResultPopulationTask();

#endregion

#region Validation

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        Assert.Equal(globalMinimumFfValue, resultPopulation.IndividualCursor.FitnessFunctionValue, tolerance);
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes();
        var genes = resultPopulation.IndividualCursor.Genes;
        for (int i = 0; i < resultPopulation.GenomeSize; i++)
            Assert.Equal(globalMinimumGenes.Span[i], genes.Span[i], tolerance);
        
        Assert.False(masterWorker.IsRunning);
        foreach (var slaveWorker in slaveWorkers)
            Assert.False(slaveWorker.IsRunning);

#endregion
    }
    
    [Fact]
    public async Task TestWithException()
    {
#region Setup

        int workersCount = Environment.ProcessorCount;

        const int populationSize = 300;
        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;
        const int throwExceptionAtEvaluationStep = 2 * populationSize + 1;
        var evaluator = new ExceptionRosenbrockEvaluator(throwExceptionAtEvaluationStep);

        const int maxGenerationNumber = 100_000;
        var terminationStrategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber);
        var context = CreateContext(workersCount,
                                    populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);
        
        var slaveWorkersCount = workersCount - 1;
        var slaveWorkers = new WorkerController[slaveWorkersCount];
        for (int i = 0; i < slaveWorkersCount; i++)
            slaveWorkers[i] = new WorkerController(workerId: i, algorithmExecutor);

        var orchestratorWorkerHandler = new OrchestratorWorkerHandler(
            slaveWorkers.ToArray(),
            context);

        var masterWorkerId = workersCount - 1;
        using var masterWorker = new WorkerController(masterWorkerId, algorithmExecutor, orchestratorWorkerHandler);

#endregion

#region Execution

        masterWorker.Start();
        foreach (var slaveWorker in slaveWorkers)
            slaveWorker.Start();

#endregion

#region Validation

        var aggregateException = await Assert.ThrowsAsync<AggregateException>(
                                     () => orchestratorWorkerHandler.GetResultPopulationTask());
        Assert.Equal(workersCount, aggregateException.InnerExceptions.Count);
        
        Assert.False(masterWorker.IsRunning);
        foreach (var slaveWorker in slaveWorkers)
            Assert.False(slaveWorker.IsRunning);

#endregion
        
        // Cleanup
        foreach (var slaveWorker in slaveWorkers)
            slaveWorker.Dispose();
    }
    
    private static ProblemContext CreateContext(
        int workersCount,
        int populationSize,
        double mutationForce,
        double crossoverProbability,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        out IAlgorithmExecutor algorithmExecutor)
    {
        var context = ProblemContextHelper.CreateContext(
            populationSize, testFitnessFunctionEvaluator, terminationStrategy, workersCount);
        var randomProvider = new RandomProvider();
        var mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability,
            populationSize: populationSize,
            lowerBound: context.GenesLowerBound,
            upperBound: context.GenesUpperBound,
            randomProvider: randomProvider);
        var selectionStrategy = new SelectionStrategy(context.GenomeSize);
        algorithmExecutor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);

        return context;
    }
}
