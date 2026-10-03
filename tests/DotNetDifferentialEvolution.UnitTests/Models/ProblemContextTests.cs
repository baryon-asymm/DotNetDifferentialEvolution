using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Common.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Models;

/// <summary>
/// Tests <see cref="DotNetDifferentialEvolution.Models.ProblemContext"/> bookkeeping:
/// buffer sizing, population swapping, and the representative-population snapshot.
/// </summary>
[Trait("Category", "Unit")]
public class ProblemContextTests
{
    private const int PopulationSize = 4;

    /// <summary>
    /// A new context sizes its population, trial-record and ranking buffers from the population size and
    /// takes the genome size from the evaluator.
    /// </summary>
    [Fact]
    public void ConstructorInitializesDerivedState()
    {
        var context = CreateContext();

        Assert.Equal(PopulationSize, context.CurrentPopulationSize);
        Assert.Equal(PopulationSize, context.TrialRecords.Length);
        Assert.Equal(PopulationSize, context.FitnessSortedIndices.Length);
        Assert.Equal(3, context.GenomeSize); // SphereEvaluator(3)
    }

    /// <summary>
    /// Swapping exchanges both the gene and the fitness buffers of the current and trial populations.
    /// </summary>
    [Fact]
    public void SwapPopulationsExchangesCurrentAndTrialBuffers()
    {
        var context = CreateContext();

        context.CurrentPopulation.Genes.Span[0] = 111.0;
        context.TrialPopulation.Genes.Span[0] = 222.0;
        context.CurrentPopulation.FfValues.Span[0] = 11.0;
        context.TrialPopulation.FfValues.Span[0] = 22.0;

        context.SwapPopulations();

        Assert.Equal(222.0, context.CurrentPopulation.Genes.Span[0]);
        Assert.Equal(111.0, context.TrialPopulation.Genes.Span[0]);
        Assert.Equal(22.0, context.CurrentPopulation.FfValues.Span[0]);
        Assert.Equal(11.0, context.TrialPopulation.FfValues.Span[0]);
    }

    /// <summary>
    /// The representative population carries the given generation number and best index and the
    /// context's evaluation count.
    /// </summary>
    [Fact]
    public void GetRepresentativePopulationStampsGenerationBestAndEvaluationCount()
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
