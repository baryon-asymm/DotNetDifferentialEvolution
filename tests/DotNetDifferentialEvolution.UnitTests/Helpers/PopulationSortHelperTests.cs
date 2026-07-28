using DotNetDifferentialEvolution.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Helpers;

/// <summary>
/// Tests fitness-based index ranking used by the p-best mutation strategies.
/// </summary>
[Trait("Category", "Unit")]
public class PopulationSortHelperTests
{
    [Fact]
    public void OrdersIndicesAscendingByFitnessBestFirst()
    {
        double[] ffValues = [3.0, 1.0, 2.0, 0.5];
        var indices = new int[4];
        var keys = new double[4];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 4, keys);

        Assert.Equal(new[] { 3, 1, 2, 0 }, indices);
    }

    [Fact]
    public void OnlyRanksTheFirstCountEntries()
    {
        double[] ffValues = [5.0, 4.0, 3.0, 2.0, 1.0];
        var indices = new[] { -1, -1, -1, -1, -1 };
        var keys = new double[5];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 3, keys);

        // First three indices (0,1,2) ranked by their fitness 5,4,3 → 2,1,0.
        Assert.Equal(new[] { 2, 1, 0 }, indices[..3]);
        // Entries beyond count are left untouched.
        Assert.Equal(new[] { -1, -1 }, indices[3..]);
    }

    [Fact]
    public void PlacesTheMinimumFirstWhenValuesAreTied()
    {
        double[] ffValues = [1.0, 1.0, 0.0];
        var indices = new int[3];
        var keys = new double[3];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 3, keys);

        Assert.Equal(2, indices[0]);                       // the strict minimum is first
        Assert.Equal(new[] { 0, 1 }, indices[1..].Order()); // the tied pair fills the rest
    }

    [Fact]
    public void RanksANaNIndividualLastRatherThanBest()
    {
        // .NET's default double comparer sorts NaN first, which would make a NaN individual the
        // top-ranked one and put it in every p-best pool.
        double[] ffValues = [3.0, double.NaN, 1.0, 2.0];
        var indices = new int[4];
        var keys = new double[4];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 4, keys);

        Assert.Equal(new[] { 2, 3, 0, 1 }, indices);
    }

    [Fact]
    public void RanksTheRealValuesCorrectlyWhenSeveralAreNaN()
    {
        double[] ffValues = [double.NaN, 2.0, double.NaN, 1.0];
        var indices = new int[4];
        var keys = new double[4];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 4, keys);

        Assert.Equal(new[] { 3, 1 }, indices[..2]);        // the finite values lead, best first
        Assert.Equal(new[] { 0, 2 }, indices[2..].Order()); // both NaN individuals trail
    }

    [Fact]
    public void StillProducesAValidPermutationWhenEveryValueIsNaN()
    {
        double[] ffValues = [double.NaN, double.NaN, double.NaN];
        var indices = new int[3];
        var keys = new double[3];

        PopulationSortHelper.SortIndicesByFitness(indices, ffValues, count: 3, keys);

        Assert.Equal(new[] { 0, 1, 2 }, indices.Order());
    }
}
