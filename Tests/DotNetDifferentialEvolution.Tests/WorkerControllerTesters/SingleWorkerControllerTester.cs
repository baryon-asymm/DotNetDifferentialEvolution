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

public class SingleWorkerControllerTester
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public SingleWorkerControllerTester(
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
}
