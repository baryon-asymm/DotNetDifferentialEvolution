using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Models;

/// <summary>
/// A <see cref="PopulationView"/> keeps the authoritative length next to the arena it describes.
/// The question it removes — "the buffer is this long, but how many individuals are live?" — is
/// the one that produced TD-3, where a population reported its allocated size and consumers read
/// individuals that had been dropped generations earlier.
/// </summary>
[Trait("Category", "Unit")]
public class PopulationViewTests
{
    [Fact]
    public void CapacityIsTheAllocatedLengthAndCountIsTheLiveOne()
    {
        var view = new PopulationView(
            Genes: new double[] { 0, 1, 2, 3, 4, 5 },
            FfValues: new double[] { 9, 1, 5 },
            Count: 2,
            GenomeSize: 2);

        Assert.Equal(3, view.Capacity);
        Assert.Equal(2, view.Count);
    }

    [Fact]
    public void GenesOfSlicesTheIndividualOutOfTheArena()
    {
        var view = new PopulationView(
            Genes: new double[] { 0, 1, 2, 3, 4, 5 },
            FfValues: new double[] { 9, 1, 5 },
            Count: 3,
            GenomeSize: 2);

        Assert.Equal(new[] { 2.0, 3.0 }, view.GenesOf(1).ToArray());
    }

    [Fact]
    public void TheActiveSpansStopAtCountRatherThanAtCapacity()
    {
        var view = new PopulationView(
            Genes: new double[] { 0, 1, 2, 3, 4, 5 },
            FfValues: new double[] { 9, 1, 5 },
            Count: 2,
            GenomeSize: 2);

        Assert.Equal(new[] { 0.0, 1.0, 2.0, 3.0 }, view.ActiveGenes.ToArray());
        Assert.Equal(new[] { 9.0, 1.0 }, view.ActiveFfValues.ToArray());
    }

    [Fact]
    public void NarrowingTheContextNarrowsBothViewsAtOnce()
    {
        // The current and trial buffers are swapped every generation, so a reduction that narrowed
        // only one of them would silently un-narrow itself at the next swap.
        var context = CreateContext();

        context.CurrentPopulationSize = 4;

        Assert.Equal(4, context.CurrentPopulation.Count);
        Assert.Equal(4, context.TrialPopulation.Count);
    }

    [Fact]
    public void SwappingKeepsBothViewsNarrowed()
    {
        var context = CreateContext();
        context.CurrentPopulationSize = 4;

        context.SwapPopulations();

        Assert.Equal(4, context.CurrentPopulationSize);
        Assert.Equal(4, context.CurrentPopulation.Count);
        Assert.Equal(4, context.TrialPopulation.Count);
    }

    [Fact]
    public void NarrowingLeavesTheBuffersAllocatedAtFullCapacity()
    {
        var context = CreateContext();

        context.CurrentPopulationSize = 4;

        Assert.Equal(PopulationSize, context.CurrentPopulation.Capacity);
        Assert.Equal(PopulationSize, context.TrialPopulation.Capacity);
    }

    private const int PopulationSize = 10;

    private static ProblemContext CreateContext()
        => ProblemContextHelper.CreateContext(
            PopulationSize,
            new SphereEvaluator(dimension: 2),
            new LimitGenerationNumberTerminationStrategy(1));
}
