using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.Tests.AlgorithmExecutionTesters;

public class AlgorithmExecutionTester
{
    private readonly ITestOutputHelper _testOutputHelper;

    public AlgorithmExecutionTester(
        ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void TestWithSimpleFitnessFunctionEvaluator()
    {
#region Setup

        var random = Random.Shared;
        var genomeSize = random.Next(1, 10);
        var lowerBounds = new double[genomeSize];
        var upperBounds = new double[genomeSize];
        for (int i = 0; i < genomeSize; i++)
        {
            lowerBounds[i] = random.NextDouble() * 10;
            upperBounds[i] = lowerBounds[i] + random.NextDouble() * 10;
        }
        
        _testOutputHelper.WriteLine($"Genome size: {genomeSize}");
        _testOutputHelper.WriteLine($"Lower bounds: {string.Join(", ", lowerBounds)}");
        _testOutputHelper.WriteLine($"Upper bounds: {string.Join(", ", upperBounds)}");

        const int populationSize = 100;
        const double mutationForce = 0.2;
        const double crossoverProbability = 0.9;
        var evaluator = new SimpleSumEvaluator(lowerBounds, upperBounds);
        const int generations = 100_000;

        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    out var algorithmExecutor);

#endregion

#region Execution

        const int workerId = 0;
        var resultPopulation = RunTestAndGetResultPopulation(workerId, generations, algorithmExecutor, context);
        
        resultPopulation.MoveCursorToBestIndividual();

#endregion

#region Validation

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes().Span;
        Assert.Equal(resultPopulation.IndividualCursor.FitnessFunctionValue, globalMinimumFfValue, tolerance);
        var genes = resultPopulation.IndividualCursor.Genes.Span;
        for (int i = 0; i < resultPopulation.GenomeSize; i++)
            Assert.Equal(genes[i], globalMinimumGenes[i], tolerance);

#endregion
    }
    
    [Fact]
    public void TestWithRosenbrockFitnessFunctionEvaluator()
    {
#region Setup

        const int populationSize = 300;
        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;
        var evaluator = new RosenbrockEvaluator();
        const int generations = 100_000;

        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    out var algorithmExecutor);

#endregion

#region Execution

        const int workerId = 0;
        var resultPopulation = RunTestAndGetResultPopulation(workerId, generations, algorithmExecutor, context);
        
        resultPopulation.MoveCursorToBestIndividual();

#endregion

#region Validation

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes().Span;
        Assert.Equal(resultPopulation.IndividualCursor.FitnessFunctionValue, globalMinimumFfValue, tolerance);
        var genes = resultPopulation.IndividualCursor.Genes.Span;
        for (int i = 0; i < resultPopulation.GenomeSize; i++)
            Assert.Equal(genes[i], globalMinimumGenes[i], tolerance);

#endregion
    }
    
    private static ProblemContext CreateContext(
        int populationSize,
        double mutationForce,
        double crossoverProbability,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        out IAlgorithmExecutor algorithmExecutor)
    {
        var context = ProblemContextHelper.CreateContext(populationSize, testFitnessFunctionEvaluator);
        var randomProvider = new RandomProvider();
        var mutationStrategy = new MutationStrategy(mutationForce, crossoverProbability, randomProvider, context);
        var selectionStrategy = new SelectionStrategy(context);
        algorithmExecutor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);
        
        return context;
    }

    private static Population RunTestAndGetResultPopulation(
        int workerId,
        int generations,
        IAlgorithmExecutor algorithmExecutor,
        ProblemContext context)
    {
        var bestHandledIndividualIndex = 0;
        for (int i = 0; i < generations; i++)
        {
            algorithmExecutor.Execute(workerId,
                                       out bestHandledIndividualIndex);

            context.SwapPopulations();
        }
        
        return context.GetRepresentativePopulation(generations, bestHandledIndividualIndex);
    }
}
