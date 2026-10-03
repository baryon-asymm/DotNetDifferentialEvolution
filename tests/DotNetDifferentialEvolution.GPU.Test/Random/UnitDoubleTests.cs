using DotNetDifferentialEvolution.GPU.Random;

namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// ACCEPTANCE.md, check 3b: the uniform doubles of <see cref="PhiloxDraws.NextUnitDouble"/> are
/// uniform on [0, 1) by a χ² test, and the largest value the 53-bit construction can give is exactly
/// <c>1 − 2⁻⁵³</c>, below 1.
/// </summary>
[Trait("Category", "Unit")]
public class UnitDoubleTests
{
    private const int DrawCount = 1_000_000;
    private const int BinCount = 100;

    /// <summary>
    /// 10⁶ draws in 100 equal bins: the χ² statistic (df = 99) stays under the 0.999 quantile,
    /// computed by <see cref="ChiSquared.Quantile"/>; every draw is in [0, 1).
    /// </summary>
    [Fact]
    public void AMillionDrawsPassTheChiSquaredTest()
    {
        var draws = new PhiloxDraws(seed: 1, individual: 0, generation: 1);
        var counts = new long[BinCount];
        for (var k = 0; k < DrawCount; k++)
        {
            var u = draws.NextUnitDouble();
            Assert.InRange(u, 0.0, Math.BitDecrement(1.0));
            counts[(int)(u * BinCount)]++;
        }

        var statistic = ChiSquared.Statistic(counts, (double)DrawCount / BinCount);
        var quantile = ChiSquared.Quantile(0.999, BinCount - 1);

        Assert.True(statistic < quantile, $"χ² = {statistic}, 0.999 quantile (df = 99) = {quantile}");
    }

    /// <summary>
    /// The analytic maximum: all 53 bits set, <c>ToUnitDouble(2³² − 1, 2³² − 1)</c>, is exactly
    /// <c>1 − 2⁻⁵³</c> and below 1; and the smallest step above 0 is exactly 2⁻⁵³.
    /// </summary>
    [Fact]
    public void TheLargestValueIsOneMinusTwoToTheMinus53()
    {
        var oneMinusUlp = 1.0 - Math.ScaleB(1.0, -53);

        var largest = DrawConversions.ToUnitDouble(uint.MaxValue, uint.MaxValue);

        Assert.Equal(oneMinusUlp, largest);
        Assert.True(largest < 1.0, $"{largest:R} is not below 1");
        Assert.Equal(0.0, DrawConversions.ToUnitDouble(0, 0));
        Assert.Equal(Math.ScaleB(1.0, -53), DrawConversions.ToUnitDouble(0, 1U << 6));
    }
}
