using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Models;

/// <summary>
/// Tests <see cref="DotNetDifferentialEvolution.Models.ProblemContext"/> bookkeeping:
/// buffer sizing, population swapping, and the representative-population snapshot.
/// </summary>
[Trait("Category", "Unit")]
public class ProblemContextTests
{
    private const int PopulationSize = 4;

    [Fact]
    public void Constructor_InitializesDerivedState()
    {
        var context = CreateContext();

        Assert.Equal(PopulationSize, context.CurrentPopulationSize);
        Assert.Equal(PopulationSize, context.TrialRecords.Length);
        Assert.Equal(PopulationSize, context.FitnessSortedIndices.Length);
        Assert.Equal(3, context.GenomeSize); // SphereEvaluator(3)
    }

    [Fact]
    public void SwapPopulations_ExchangesCurrentAndTrialBuffers()
    {
        var context = CreateContext();

        context.Population.Span[0] = 111.0;
        context.TrialPopulation.Span[0] = 222.0;
        context.PopulationFfValues.Span[0] = 11.0;
        context.TrialPopulationFfValues.Span[0] = 22.0;

        context.SwapPopulations();

        Assert.Equal(222.0, context.Population.Span[0]);
        Assert.Equal(111.0, context.TrialPopulation.Span[0]);
        Assert.Equal(22.0, context.PopulationFfValues.Span[0]);
        Assert.Equal(11.0, context.TrialPopulationFfValues.Span[0]);
    }

    [Fact]
    public void GetRepresentativePopulation_StampsGenerationBestAndEvaluationCount()
    {
        var context = CreateContext();
        context.EvaluationCount = 1234;

        var population = context.GetRepresentativePopulation(generationNumber: 7, bestIndividualIndex: 2);

        Assert.Equal(7, population.GenerationNumber);
        Assert.Equal(2, population.BestIndividualIndex);
        Assert.Equal(1234, population.EvaluationCount);
    }

    private static DotNetDifferentialEvolution.Models.ProblemContext CreateContext()
    {
        var evaluator = new SphereEvaluator(dimension: 3);
        var termination = new LimitGenerationNumberTerminationStrategy(1);
        return ProblemContextHelper.CreateContext(PopulationSize, evaluator, termination);
    }
}
