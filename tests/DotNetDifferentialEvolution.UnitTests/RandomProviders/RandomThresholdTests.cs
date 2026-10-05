using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.UnitTests.RandomProviders;

/// <summary>
/// The per-gene crossover test used to be <c>NextDouble() &lt;= CR</c> and is now
/// <c>NextULong() &lt;= Scale(CR)</c>. That substitution is only sound if scaling preserves the
/// order of the comparison, so these tests pin exactly that, plus the two ends of the range
/// where a naive conversion overflows.
/// </summary>
[Trait("Category", "Unit")]
public class RandomThresholdTests
{
    /// <summary>
    /// For many random pairs of draw and probability, the integer comparison of the scaled values gives
    /// the same decision as the floating-point comparison it replaces.
    /// </summary>
    [Fact]
    public void ScalingPreservesTheOrderOfTheComparisonItReplaces()
    {
        var random = new SeededRandomProvider(4242);

        for (var i = 0; i < 200_000; i++)
        {
            var draw = random.NextDouble();
            var probability = random.NextDouble();

            var floatingPointDecision = draw <= probability;
            var integerDecision = RandomThreshold.Scale(draw) <= RandomThreshold.Scale(probability);

            Assert.Equal(floatingPointDecision, integerDecision);
        }
    }

    /// <summary>
    /// A probability of 1 scales to the largest <see cref="ulong"/>, so every draw passes the test.
    /// </summary>
    [Fact]
    public void AProbabilityOfOneAcceptsEveryDraw()
    {
        var threshold = RandomThreshold.Scale(1.0);

        Assert.Equal(ulong.MaxValue, threshold);
        Assert.True(RandomThreshold.Scale(Math.BitDecrement(1.0)) <= threshold);
    }

    /// <summary>
    /// Exactly 1.0 is clamped to the largest <see cref="ulong"/>, while the largest double below 1.0
    /// scales exactly to 2^64 - 2^11.
    /// </summary>
    [Fact]
    public void OneIsTheOnlyInRangeValueThatNeedsClamping()
    {
        // 2^64 is not representable as a ulong, and converting an out-of-range double is
        // unspecified — so a CR of exactly 1.0, which any constant-parameter strategy may be
        // configured with, has to be clamped rather than cast.
        Assert.Equal(ulong.MaxValue, RandomThreshold.Scale(1.0));

        // Everything strictly below 1.0 scales exactly: 1 - 2^-53 lands on 2^64 - 2^11, which a
        // double represents without rounding, so the clamp is not doing the work here.
        var largestBelowOne = Math.BitDecrement(1.0);

        Assert.Equal(ulong.MaxValue - 2047UL, RandomThreshold.Scale(largestBelowOne));
        Assert.True(RandomThreshold.Scale(largestBelowOne) < ulong.MaxValue);
    }

    /// <summary>
    /// A probability of 0 scales to zero, so only a zero draw passes and the smallest positive draw
    /// fails.
    /// </summary>
    [Fact]
    public void AProbabilityOfZeroAcceptsOnlyTheZeroDraw()
    {
        Assert.Equal(0UL, RandomThreshold.Scale(0.0));
        Assert.True(RandomThreshold.Scale(0.0) <= RandomThreshold.Scale(0.0));

        // Anything the generator can actually return above zero is rejected: the smallest draw
        // NextDouble can produce short of zero is 2^-53, which scales to 2^11.
        Assert.False(RandomThreshold.Scale(Math.Pow(2.0, -53)) <= RandomThreshold.Scale(0.0));
    }

    /// <summary>
    /// Values below 0 clamp to zero and values above 1 clamp to the largest <see cref="ulong"/>.
    /// </summary>
    [Fact]
    public void NegativeAndOutOfRangeValuesAreClamped()
    {
        Assert.Equal(0UL, RandomThreshold.Scale(-1.0));
        Assert.Equal(ulong.MaxValue, RandomThreshold.Scale(2.0));
    }
}
