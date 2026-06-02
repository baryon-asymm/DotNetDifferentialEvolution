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

    [Theory]
    [InlineData(0.0, 0.2)]    // min must be > 0
    [InlineData(-0.1, 0.2)]   // min out of range
    [InlineData(0.05, 1.1)]   // max out of range
    public void RangeConstructor_ThrowsWhenRatesAreOutOfRange(
        double pBestRateMin,
        double pBestRateMax)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CurrentToPBestMutationStrategy(pBestRateMin, pBestRateMax));
    }

    [Fact]
    public void RangeConstructor_ThrowsWhenMinExceedsMax()
    {
        Assert.Throws<ArgumentException>(
            () => new CurrentToPBestMutationStrategy(pBestRateMin: 0.3, pBestRateMax: 0.2));
    }

    [Fact]
    public void RangeConstructor_AcceptsAValidPerIndividualRange()
    {
        var strategy = new CurrentToPBestMutationStrategy(pBestRateMin: 0.05, pBestRateMax: 0.2);

        Assert.NotNull(strategy);
    }
}
