using DotNetDifferentialEvolution.MutationStrategies;

namespace DotNetDifferentialEvolution.UnitTests.MutationStrategies;

/// <summary>
/// Tests the constructor guard of the current-to-pbest strategy used by JADE/SHADE/L-SHADE.
/// </summary>
[Trait("Category", "Unit")]
public class CurrentToPBestMutationStrategyTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_ThrowsWhenPBestRateIsOutOfRange(
        double pBestRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CurrentToPBestMutationStrategy(pBestRate));
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_AcceptsRatesInTheHalfOpenInterval(
        double pBestRate)
    {
        var strategy = new CurrentToPBestMutationStrategy(pBestRate);

        Assert.NotNull(strategy);
    }
}
