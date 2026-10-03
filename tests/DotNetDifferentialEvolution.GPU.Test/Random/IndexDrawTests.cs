using System.Numerics;
using DotNetDifferentialEvolution.GPU.Random;

namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// ACCEPTANCE.md, check 3c: <see cref="DrawConversions.ToIndex"/> is Lemire's multiply-shift,
/// exactly <c>⌊word · n / 2³²⌋</c> on scripted 32-bit words. The closed form is evaluated in
/// <see cref="BigInteger"/> arithmetic, which shares nothing with the 64-bit code under test.
/// </summary>
[Trait("Category", "Unit")]
public class IndexDrawTests
{
    private static readonly uint[] Words =
    [
        0U, 1U, 2U, 3U, 0x7FFF_FFFFU, 0x8000_0000U, 0x8000_0001U, 0xFFFF_FFFEU, uint.MaxValue,
        0x1234_5678U, 0x9E37_79B9U, 0xDEAD_BEEFU, 0x5555_5555U, 0xAAAA_AAAAU, 1_000_000U, 4_000_000_000U,
    ];

    private static readonly int[] Sizes = [1, 2, 3, 4, 7, 49, 50, 64, 1000, 10_000, int.MaxValue];

    /// <summary>Every scripted word and size gives the closed form.</summary>
    [Fact]
    public void ScriptedWordsGiveTheClosedForm()
    {
        var twoToThe32 = BigInteger.One << 32;
        foreach (var n in Sizes)
        {
            foreach (var word in Words)
            {
                var closedForm = (int)(new BigInteger(word) * n / twoToThe32);

                Assert.True(closedForm == DrawConversions.ToIndex(word, n), $"word {word:X8}, n {n}: expected {closedForm}");
            }
        }
    }

    /// <summary>Hand-computed answers: the ends of the word range and the midpoint.</summary>
    [Fact]
    public void TheEndsAndTheMiddleOfTheRangeAreHandComputed()
    {
        Assert.Equal(0, DrawConversions.ToIndex(0U, 49));
        Assert.Equal(0, DrawConversions.ToIndex(1U, 3));
        Assert.Equal(1, DrawConversions.ToIndex(0x8000_0000U, 3));
        Assert.Equal(24, DrawConversions.ToIndex(0x8000_0000U, 49));
        Assert.Equal(5000, DrawConversions.ToIndex(0x8000_0000U, 10_000));
        Assert.Equal(48, DrawConversions.ToIndex(uint.MaxValue, 49));
        Assert.Equal(9999, DrawConversions.ToIndex(uint.MaxValue, 10_000));
        Assert.Equal(0, DrawConversions.ToIndex(uint.MaxValue, 1));
    }
}
