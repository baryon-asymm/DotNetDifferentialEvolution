using DotNetDifferentialEvolution.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Helpers;

/// <summary>
/// Tests fitness-based index ranking used by the p-best mutation strategies.
/// </summary>
[Trait("Category", "Unit")]
public class PopulationSortHelperTests
{
    private static readonly int[] TiedPairIndices = [0, 1];
    private static readonly int[] NaNIndividualIndices = [0, 2];
    private static readonly int[] EveryIndexOfThree = [0, 1, 2];

    /// <summary>
    /// The indices come out ordered by ascending fitness, the best individual first.
    /// </summary>
    [Fact]
    public void OrdersIndicesAscendingByFitnessBestFirst()
    {
        double[] ffValues = [3.0, 1.0, 2.0, 0.5];
        var indices = new int[4];
        var keys = new double[4];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 4, keys);

        Assert.Equal([3, 1, 2, 0], indices);
    }

    /// <summary>
    /// Only the first <c>count</c> individuals are ranked; index entries beyond <c>count</c> are left
    /// untouched.
    /// </summary>
    [Fact]
    public void OnlyRanksTheFirstCountEntries()
    {
        double[] ffValues = [5.0, 4.0, 3.0, 2.0, 1.0];
        var indices = new[] { -1, -1, -1, -1, -1 };
        var keys = new double[5];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 3, keys);

        // First three indices (0,1,2) ranked by their fitness 5,4,3 → 2,1,0.
        Assert.Equal([2, 1, 0], indices[..3]);
        // Entries beyond count are left untouched.
        Assert.Equal([-1, -1], indices[3..]);
    }

    /// <summary>
    /// With tied values present, the strict minimum still comes first and the tied individuals fill the
    /// remaining places.
    /// </summary>
    [Fact]
    public void PlacesTheMinimumFirstWhenValuesAreTied()
    {
        double[] ffValues = [1.0, 1.0, 0.0];
        var indices = new int[3];
        var keys = new double[3];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 3, keys);

        Assert.Equal(2, indices[0]);                       // the strict minimum is first
        Assert.Equal(TiedPairIndices, indices[1..].Order()); // the tied pair fills the rest
    }

    /// <summary>
    /// An individual whose fitness is NaN is ranked last, not first as the default double ordering
    /// would place it.
    /// </summary>
    [Fact]
    public void RanksANaNIndividualLastRatherThanBest()
    {
        // .NET's default double comparer sorts NaN first, which would make a NaN individual the
        // top-ranked one and put it in every p-best pool.
        double[] ffValues = [3.0, double.NaN, 1.0, 2.0];
        var indices = new int[4];
        var keys = new double[4];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 4, keys);

        Assert.Equal([2, 3, 0, 1], indices);
    }

    /// <summary>
    /// With several NaN values, the finite values still lead in ascending order and all NaN individuals
    /// trail.
    /// </summary>
    [Fact]
    public void RanksTheRealValuesCorrectlyWhenSeveralAreNaN()
    {
        double[] ffValues = [double.NaN, 2.0, double.NaN, 1.0];
        var indices = new int[4];
        var keys = new double[4];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 4, keys);

        Assert.Equal([3, 1], indices[..2]);        // the finite values lead, best first
        Assert.Equal(NaNIndividualIndices, indices[2..].Order()); // both NaN individuals trail
    }

    /// <summary>
    /// When every value is NaN, the result is still a permutation of all indices.
    /// </summary>
    [Fact]
    public void StillProducesAValidPermutationWhenEveryValueIsNaN()
    {
        double[] ffValues = [double.NaN, double.NaN, double.NaN];
        var indices = new int[3];
        var keys = new double[3];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 3, keys);

        Assert.Equal(EveryIndexOfThree, indices.Order());
    }
}
