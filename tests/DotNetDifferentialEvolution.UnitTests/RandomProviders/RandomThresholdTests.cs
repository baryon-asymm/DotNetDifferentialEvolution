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
    [Fact]
    public void ScalingPreservesTheOrderOfTheComparisonItReplaces()
    {
        var random = new Random(4242);

        for (int i = 0; i < 200_000; i++)
        {
            var draw = random.NextDouble();
            var probability = random.NextDouble();

            var floatingPointDecision = draw <= probability;
            var integerDecision = RandomThreshold.Scale(draw) <= RandomThreshold.Scale(probability);

            Assert.Equal(floatingPointDecision, integerDecision);
        }
    }

    [Fact]
    public void AProbabilityOfOneAcceptsEveryDraw()
    {
        var threshold = RandomThreshold.Scale(1.0);

        Assert.Equal(ulong.MaxValue, threshold);
        Assert.True(RandomThreshold.Scale(Math.BitDecrement(1.0)) <= threshold);
    }

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

    [Fact]
    public void AProbabilityOfZeroAcceptsOnlyTheZeroDraw()
    {
        Assert.Equal(0UL, RandomThreshold.Scale(0.0));
        Assert.True(RandomThreshold.Scale(0.0) <= RandomThreshold.Scale(0.0));

        // Anything the generator can actually return above zero is rejected: the smallest draw
        // NextDouble can produce short of zero is 2^-53, which scales to 2^11.
        Assert.False(RandomThreshold.Scale(Math.Pow(2.0, -53)) <= RandomThreshold.Scale(0.0));
    }

    [Fact]
    public void NegativeAndOutOfRangeValuesAreClamped()
    {
        Assert.Equal(0UL, RandomThreshold.Scale(-1.0));
        Assert.Equal(ulong.MaxValue, RandomThreshold.Scale(2.0));
    }
}
