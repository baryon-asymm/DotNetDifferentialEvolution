using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
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
    public void TestWithSimpleSumFitnessFunctionEvaluator()
    {
#region Setup

        var random = Random.Shared;
        var genomeSize = random.Next(2, 20);
        var lowerBounds = new double[genomeSize];
        var upperBounds = new double[genomeSize];
        for (int i = 0; i < genomeSize; i++)
        {
            lowerBounds[i] = random.Next(-50, 50) * 1.0;
            upperBounds[i] = lowerBounds[i] + random.Next(10) * 10.0;
        }
        
        _testOutputHelper.WriteLine($"Genome size: {genomeSize}");
        _testOutputHelper.WriteLine($"Lower bounds: {string.Join(", ", lowerBounds)}");
        _testOutputHelper.WriteLine($"Upper bounds: {string.Join(", ", upperBounds)}");

        const int populationSize = 200;
        const double mutationForce = 0.3;
        const double crossoverProbability = 0.8;
        var evaluator = new SimpleSumEvaluator(lowerBounds, upperBounds);

        const int maxGenerationNumber = 100_000;
        var terminationStrategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber);
        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);

#endregion

#region Execution

        const int workerId = 0;
        var resultPopulation = RunTestAndGetResultPopulation(workerId, algorithmExecutor, context);
        
        resultPopulation.MoveCursorToBestIndividual();

#endregion

#region Validation

        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        var error = Math.Pow(resultPopulation.IndividualCursor.FitnessFunctionValue - globalMinimumFfValue, 2)
            / Math.Pow(globalMinimumFfValue, 2);
        const double onePercent = 0.01;
        Assert.True(error <= onePercent);

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

        const int maxStagnationStreak = 1000;
        const double stagnationThreshold = 1e-6;
        var terminationStrategy = new StagnationStreakTerminationStrategy(maxStagnationStreak, stagnationThreshold);
        var context = CreateContext(populationSize,
                                    mutationForce,
                                    crossoverProbability,
                                    evaluator,
                                    terminationStrategy,
                                    out var algorithmExecutor);

#endregion

#region Execution

        const int workerId = 0;
        var resultPopulation = RunTestAndGetResultPopulation(workerId, algorithmExecutor, context);
        
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

    private static Population RunTestAndGetResultPopulation(
        int workerId,
        IAlgorithmExecutor algorithmExecutor,
        ProblemContext context)
    {
        var terminationStrategy = context.TerminationStrategy;
        
        Population population;
        var generationNumber = 0;
        var bestHandledIndividualIndex = 0;
        do
        {
            algorithmExecutor.Execute(workerId,
                                      out bestHandledIndividualIndex);

            context.SwapPopulations();
            
            population = context.GetRepresentativePopulation(++generationNumber, bestHandledIndividualIndex);
        } while (terminationStrategy.ShouldTerminate(population) == false);
        
        return population;
    }
}
