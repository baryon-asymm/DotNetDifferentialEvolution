using DotNetDifferentialEvolution.UnitTests.TestSupport;

namespace DotNetDifferentialEvolution.UnitTests.Models;

/// <summary>
/// Tests the <see cref="DotNetDifferentialEvolution.Models.Population"/> view: derived sizes
/// and cursor movement.
/// </summary>
[Trait("Category", "Unit")]
public class PopulationTests
{
    [Fact]
    public void DerivesPopulationAndGenomeSizeFromBuffers()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        Assert.Equal(3, population.PopulationSize);
        Assert.Equal(2, population.GenomeSize);
    }

    [Fact]
    public void MoveCursorTo_PointsCursorAtTheRequestedIndividual()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        population.MoveCursorTo(2);

        Assert.Equal(5.0, population.IndividualCursor.FitnessFunctionValue);
        Assert.Equal(new[] { 4.0, 5.0 }, population.IndividualCursor.Genes.ToArray());
    }

    [Fact]
    public void MoveCursorToBestIndividual_UsesBestIndividualIndex()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0], bestIndividualIndex: 1);

        population.MoveCursorToBestIndividual();

        Assert.Equal(1.0, population.IndividualCursor.FitnessFunctionValue);
        Assert.Equal(new[] { 2.0, 3.0 }, population.IndividualCursor.Genes.ToArray());
    }

    [Fact]
    public void APopulationStartsFullyActive()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        Assert.Equal(population.Capacity, population.PopulationSize);
    }

    [Fact]
    public void GenomeSizeStaysDerivedFromTheCapacityWhenThePopulationShrinks()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        population.PopulationSize = 1;

        Assert.Equal(3, population.Capacity);
        Assert.Equal(2, population.GenomeSize);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public void MoveCursorTo_RefusesAnIndexOutsideTheActivePopulation(
        int individualIndex)
    {
        // Index 1 and 2 are allocated but no longer live once the population is reduced to one;
        // handing them back would be exactly the stale-individual bug this guards.
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        population.PopulationSize = 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => population.MoveCursorTo(individualIndex));
    }
}
