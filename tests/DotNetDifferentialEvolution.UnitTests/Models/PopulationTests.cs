using DotNetDifferentialEvolution.UnitTests.TestSupport;

namespace DotNetDifferentialEvolution.UnitTests.Models;

/// <summary>
/// Tests the <see cref="DotNetDifferentialEvolution.Models.Population"/> view: derived sizes
/// and cursor movement.
/// </summary>
[Trait("Category", "Unit")]
public class PopulationTests
{
    /// <summary>
    /// The population size is the length of the fitness buffer and the genome size is the gene buffer
    /// divided by it.
    /// </summary>
    [Fact]
    public void DerivesPopulationAndGenomeSizeFromBuffers()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        Assert.Equal(3, population.PopulationSize);
        Assert.Equal(2, population.GenomeSize);
    }

    /// <summary>
    /// Moving the cursor to an index exposes that individual's fitness value and genes.
    /// </summary>
    [Fact]
    public void MoveCursorToPointsCursorAtTheRequestedIndividual()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        population.MoveCursorTo(2);

        Assert.Equal(5.0, population.IndividualCursor.FitnessFunctionValue);
        Assert.Equal([4.0, 5.0], population.IndividualCursor.Genes.ToArray());
    }

    /// <summary>
    /// Moving the cursor to the best individual uses the recorded best index.
    /// </summary>
    [Fact]
    public void MoveCursorToBestIndividualUsesBestIndividualIndex()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0], bestIndividualIndex: 1);

        population.MoveCursorToBestIndividual();

        Assert.Equal(1.0, population.IndividualCursor.FitnessFunctionValue);
        Assert.Equal([2.0, 3.0], population.IndividualCursor.Genes.ToArray());
    }

    /// <summary>
    /// A new population's active size equals its capacity.
    /// </summary>
    [Fact]
    public void APopulationStartsFullyActive()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        Assert.Equal(population.Capacity, population.PopulationSize);
    }

    /// <summary>
    /// Shrinking the active population keeps the capacity and the genome size unchanged.
    /// </summary>
    [Fact]
    public void GenomeSizeStaysDerivedFromTheCapacityWhenThePopulationShrinks()
    {
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        population.PopulationSize = 1;

        Assert.Equal(3, population.Capacity);
        Assert.Equal(2, population.GenomeSize);
    }

    /// <summary>
    /// After the population is reduced, the cursor refuses an index that is negative or outside the
    /// active individuals, even when the slot is still allocated.
    /// </summary>
    /// <param name="individualIndex">The index outside the active population.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(3)]
    public void MoveCursorToRefusesAnIndexOutsideTheActivePopulation(
        int individualIndex)
    {
        // Index 1 and 2 are allocated but no longer live once the population is reduced to one;
        // handing them back would be exactly the stale-individual bug this guards.
        var population = PopulationFactory.Create(
            genes: [0, 1, 2, 3, 4, 5], fitnessValues: [9.0, 1.0, 5.0]);

        population.PopulationSize = 1;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => population.MoveCursorTo(individualIndex));
    }
}
