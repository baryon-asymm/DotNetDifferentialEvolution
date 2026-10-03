using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.UnitTests.TestSupport;

namespace DotNetDifferentialEvolution.UnitTests.TerminationStrategies;

/// <summary>
/// Tests the stagnation-streak strategy: it terminates after a configured number of
/// consecutive generations without a fitness improvement greater than the threshold.
/// </summary>
[Trait("Category", "Unit")]
public class StagnationStreakTerminationStrategyTests
{
    /// <summary>
    /// After the baseline generation, each generation without improvement extends the streak, and the
    /// strategy terminates when the streak reaches its maximum.
    /// </summary>
    [Fact]
    public void AccumulatesStreakAndTerminatesAfterMaxStagnantGenerations()
    {
        var strategy = new StagnationStreakTerminationStrategy(maxStagnationStreak: 3, stagnationThreshold: 1e-6);
        var ff = new[] { 10.0 };
        var population = PopulationFactory.SingleIndividual(ff);

        // First evaluation establishes the baseline (streak resets to 0).
        Assert.False(strategy.ShouldTerminate(population));
        Assert.Equal(0, strategy.CurrentStagnationStreak);

        // No improvement → streak grows: 1, 2, then 3 fires.
        Assert.False(strategy.ShouldTerminate(population)); // streak 1
        Assert.False(strategy.ShouldTerminate(population)); // streak 2
        Assert.True(strategy.ShouldTerminate(population));  // streak 3 >= max
        Assert.Equal(3, strategy.CurrentStagnationStreak);
    }

    /// <summary>
    /// An improvement larger than the threshold resets the streak and becomes the new baseline.
    /// </summary>
    [Fact]
    public void ImprovementGreaterThanThresholdResetsTheStreak()
    {
        var strategy = new StagnationStreakTerminationStrategy(maxStagnationStreak: 3, stagnationThreshold: 1e-6);
        var ff = new[] { 10.0 };
        var population = PopulationFactory.SingleIndividual(ff);

        _ = strategy.ShouldTerminate(population); // baseline
        _ = strategy.ShouldTerminate(population); // streak 1
        _ = strategy.ShouldTerminate(population); // streak 2
        Assert.Equal(2, strategy.CurrentStagnationStreak);

        // A meaningful improvement resets the streak.
        ff[0] = 5.0;
        Assert.False(strategy.ShouldTerminate(population));
        Assert.Equal(0, strategy.CurrentStagnationStreak);
        Assert.Equal(5.0, strategy.LastBestFitnessFunctionValue);
    }

    /// <summary>
    /// An improvement smaller than the threshold counts as stagnation and does not move the baseline.
    /// </summary>
    [Fact]
    public void ImprovementSmallerThanThresholdDoesNotResetTheStreak()
    {
        var strategy = new StagnationStreakTerminationStrategy(maxStagnationStreak: 5, stagnationThreshold: 1e-3);
        var ff = new[] { 1.0 };
        var population = PopulationFactory.SingleIndividual(ff);

        _ = strategy.ShouldTerminate(population); // baseline, last = 1.0
        // Tiny change below the threshold counts as stagnation.
        ff[0] = 1.0 - 1e-5;
        _ = strategy.ShouldTerminate(population);
        Assert.Equal(1, strategy.CurrentStagnationStreak);
        // Baseline is not updated for sub-threshold changes.
        Assert.Equal(1.0, strategy.LastBestFitnessFunctionValue);
    }
}
