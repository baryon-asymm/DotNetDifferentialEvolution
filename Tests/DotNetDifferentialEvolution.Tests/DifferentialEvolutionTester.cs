using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.Tests;

public class DifferentialEvolutionTester
{
    private readonly ITestOutputHelper _testOutputHelper;
    
    public DifferentialEvolutionTester(
        ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    
    [Fact]
    public async Task TestSimpleSum()
    {
#region Setup

        var random = Random.Shared;
        var genomeSize = random.Next(2, 100);
        var lowerBounds = new double[genomeSize];
        var upperBounds = new double[genomeSize];
        for (int i = 0; i < genomeSize; i++)
        {
            lowerBounds[i] = random.Next(-100, 100) * 1.0;
            upperBounds[i] = lowerBounds[i] + random.Next(10) * 10.0;
        }
        
        _testOutputHelper.WriteLine($"Genome size: {genomeSize}");
        _testOutputHelper.WriteLine($"Lower bounds: {string.Join(", ", lowerBounds)}");
        _testOutputHelper.WriteLine($"Upper bounds: {string.Join(", ", upperBounds)}");

        const int populationSize = 1000;
        const double mutationForce = 0.3;
        const double crossoverProbability = 0.8;
        var evaluator = new SimpleSumEvaluator(lowerBounds, upperBounds);
        
        const int maxGenerationNumber = 100_000;

        using var differentialEvolution = DifferentialEvolutionBuilder
                                          .ForFunction(evaluator)
                                          .WithBounds(lowerBounds, upperBounds)
                                          .WithPopulationSize(populationSize)
                                          .WithUniformPopulationSampling()
                                          .WithDefaultMutationStrategy(mutationForce, crossoverProbability)
                                          .WithDefaultSelectionStrategy()
                                          .WithTerminationCondition(
                                              new LimitGenerationNumberTerminationStrategy(maxGenerationNumber))
                                          .UseAllProcessors()
                                          .Build();

#endregion

#region Execution

        var result = await differentialEvolution.RunAsync();

#endregion

#region Validation

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        Assert.Equal(globalMinimumFfValue, result.IndividualCursor.FitnessFunctionValue, tolerance);
        
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes();
        var genes = result.IndividualCursor.Genes;
        for (int i = 0; i < result.GenomeSize; i++)
            Assert.Equal(globalMinimumGenes.Span[i], genes.Span[i], tolerance);

#endregion
    }

    [Fact]
    public async Task TestRosenbrock()
    {
#region Setup

        const int populationSize = 200;
        const double mutationForce = 0.3;
        const double crossoverProbability = 0.8;
        
        var evaluator = new RosenbrockEvaluator();
        
        var lowerBounds = evaluator.GetLowerBounds();
        var upperBounds = evaluator.GetUpperBounds();
        
        const int maxStagnationStreak = 100_000;
        const double stagnationThreshold = 1e-6;

        using var differentialEvolution = DifferentialEvolutionBuilder
                                          .ForFunction(evaluator)
                                          .WithBounds(lowerBounds, upperBounds)
                                          .WithPopulationSize(populationSize)
                                          .WithUniformPopulationSampling()
                                          .WithDefaultMutationStrategy(mutationForce, crossoverProbability)
                                          .WithDefaultSelectionStrategy()
                                          .WithTerminationCondition(
                                              new StagnationStreakTerminationStrategy(
                                                  maxStagnationStreak: maxStagnationStreak,
                                                  stagnationThreshold: stagnationThreshold))
                                          .UseAllProcessors()
                                          .Build();

#endregion

#region Execution

        var result = await differentialEvolution.RunAsync();

#endregion

#region Validation

        const double tolerance = 1e-6;
        var globalMinimumFfValue = evaluator.GetGlobalMinimumFfValue();
        Assert.Equal(globalMinimumFfValue, result.IndividualCursor.FitnessFunctionValue, tolerance);
        
        var globalMinimumGenes = evaluator.GetGlobalMinimumGenes();
        var genes = result.IndividualCursor.Genes;
        for (int i = 0; i < result.GenomeSize; i++)
            Assert.Equal(globalMinimumGenes.Span[i], genes.Span[i], tolerance);

#endregion
    }
}
