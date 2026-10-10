using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, check A3, the half that runs in CI: <see cref="FitnessOrder.OrderKey"/>
/// is the order of <see cref="FitnessOrder.KeyOf"/> as an integer, and <see cref="FitnessOrder.Precedes(long, int, long, int)"/>
/// orders by (key, index). Its known answers are the frozen ones: −∞, −<see cref="double.MaxValue"/>, −1, −ε (the smallest
/// subnormal), −0, +0, ε, 1, <see cref="double.MaxValue"/>, +∞ and <see cref="double.NaN"/>, non-decreasing, with −0 = +0
/// and +∞ = NaN.
/// </summary>
[Trait("Category", "Unit")]
public class FitnessOrderTests
{
    private const int RandomPairs = 20000;
    private const int CaseSeed = 20261020;

    /// <summary>A3: each known answer's key is above its predecessor's, or equal where it must be (−0 = +0, +∞ = NaN).</summary>
    [Fact]
    public void TheKnownAnswersAreOrderedWithZerosAndInfinitiesEqual()
    {
        // The frozen known answers, ascending, each with whether its key must equal its predecessor's.
        (double Value, bool EqualsPredecessor)[] answers =
        [
            (double.NegativeInfinity, false),
            (-double.MaxValue, false),
            (-1.0, false),
            (-double.Epsilon, false),
            (-0.0, false),
            (0.0, true),
            (double.Epsilon, false),
            (1.0, false),
            (double.MaxValue, false),
            (double.PositiveInfinity, false),
            (double.NaN, true),
        ];
        for (var at = 1; at < answers.Length; at++)
        {
            var previous = FitnessOrder.OrderKey(answers[at - 1].Value);
            var key = FitnessOrder.OrderKey(answers[at].Value);
            if (answers[at].EqualsPredecessor)
            {
                Assert.True(previous == key, $"the key of {answers[at - 1].Value:R} ({previous}) is not the key of {answers[at].Value:R} ({key})");
            }
            else
            {
                Assert.True(previous < key, $"the key of {answers[at - 1].Value:R} ({previous}) is not below the key of {answers[at].Value:R} ({key})");
            }
        }
    }

    /// <summary>A3: every NaN, whatever its sign and payload, has the key of +∞.</summary>
    /// <param name="bits">The NaN's bits.</param>
    [Theory]
    [InlineData(0x7FF8000000000000UL)]
    [InlineData(0xFFF8000000000000UL)]
    [InlineData(0x7FF0000000000001UL)]
    [InlineData(0xFFFFFFFFFFFFFFFFUL)]
    public void EveryNaNHasTheKeyOfInfinity(ulong bits)
    {
        var nan = BitConverter.Int64BitsToDouble(unchecked((long)bits));
        Assert.True(double.IsNaN(nan));
        Assert.Equal(FitnessOrder.OrderKey(double.PositiveInfinity), FitnessOrder.OrderKey(nan));
        Assert.Equal(FitnessOrder.InfinityKey, FitnessOrder.OrderKey(nan));
    }

    /// <summary>A3: on random bit patterns, the integer keys compare as <see cref="FitnessOrder.KeyOf"/> compares.</summary>
    [Fact]
    public void TheKeysCompareAsTheDoubleKeysDo()
    {
        var random = new SeededRandomProvider(CaseSeed);
        for (var pair = 0; pair < RandomPairs; pair++)
        {
            var a = RandomValue(random);
            var b = RandomValue(random);
            var expected = Math.Sign(FitnessOrder.KeyOf(a).CompareTo(FitnessOrder.KeyOf(b)));
            var actual = Math.Sign(FitnessOrder.OrderKey(a).CompareTo(FitnessOrder.OrderKey(b)));
            Assert.True(expected == actual, $"{a:R} against {b:R}: the double keys give {expected}, the integer keys {actual}");
        }
    }

    /// <summary>A3: an entry precedes another by a lower key, or an equal key and a lower index, and never itself.</summary>
    [Fact]
    public void PrecedesOrdersByKeyThenIndex()
    {
        Assert.True(FitnessOrder.Precedes(1L, 5, 2L, 0));
        Assert.False(FitnessOrder.Precedes(2L, 0, 1L, 5));
        Assert.True(FitnessOrder.Precedes(7L, 1, 7L, 2));
        Assert.False(FitnessOrder.Precedes(7L, 2, 7L, 1));
        Assert.False(FitnessOrder.Precedes(7L, 3, 7L, 3));
        Assert.True(FitnessOrder.Precedes(FitnessOrder.OrderKey(-0.0), 4, FitnessOrder.OrderKey(0.0), 9));
        Assert.False(FitnessOrder.Precedes(FitnessOrder.OrderKey(0.0), 9, FitnessOrder.OrderKey(-0.0), 4));
        Assert.True(FitnessOrder.Precedes(FitnessOrder.OrderKey(double.PositiveInfinity), 2, FitnessOrder.OrderKey(double.NaN), 8));
    }

    private static double RandomValue(SeededRandomProvider random)
    {
        // Three draws of 31 bits cover all 64 bits of a double's pattern but the top bit of the lowest two; a
        // quarter of the values come from a small set so that equal keys and the specials occur.
        if (random.Next(4) == 0)
        {
            double[] specials = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, 1.0, -1.0, double.Epsilon, -double.Epsilon];
            return specials[random.Next(specials.Length)];
        }

        var bits = (long)random.Next(int.MaxValue) << 40 ^ (long)random.Next(int.MaxValue) << 20 ^ random.Next(int.MaxValue);
        return BitConverter.Int64BitsToDouble(bits);
    }
}
