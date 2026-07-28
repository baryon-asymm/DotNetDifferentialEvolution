using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.UnitTests.RandomProviders;

/// <summary>
/// The provider every worker draws from. It is the engine's only source of randomness, so its
/// output has to be uniform, its stream has to be a function of the seed alone, and adjacent
/// seeds — which is exactly how the engine derives worker seeds — have to give unrelated streams.
/// </summary>
[Trait("Category", "Unit")]
public class SeededRandomProviderTests
{
    [Fact]
    public void SameSeedProducesTheSameStream()
    {
        var first = new SeededRandomProvider(seed: 12345);
        var second = new SeededRandomProvider(seed: 12345);

        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(first.NextULong(), second.NextULong());
            Assert.Equal(first.NextDouble(), second.NextDouble());
            Assert.Equal(first.Next(1_000), second.Next(1_000));
        }
    }

    [Fact]
    public void DifferentSeedsProduceDifferentStreams()
    {
        var first = new SeededRandomProvider(seed: 12345);
        var second = new SeededRandomProvider(seed: 12346);

        var collisions = 0;
        for (int i = 0; i < 1_000; i++)
        {
            if (first.NextULong() == second.NextULong())
                collisions++;
        }

        // Adjacent seeds are what the engine hands consecutive workers. SplitMix64 expansion is
        // what keeps those streams apart; a generator seeded by copying would fail here.
        Assert.Equal(0, collisions);
    }

    [Fact]
    public void NextDoubleStaysInTheUnitInterval()
    {
        var random = new SeededRandomProvider(seed: 7);

        for (int i = 0; i < 100_000; i++)
        {
            var value = random.NextDouble();
            Assert.True(value >= 0.0 && value < 1.0, $"NextDouble returned {value}");
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(400)]
    public void NextIsUniformOverTheRequestedRange(
        int maxValue)
    {
        const int DrawsPerBucket = 2_000;

        var random = new SeededRandomProvider(seed: maxValue);
        var counts = new int[maxValue];
        var draws = maxValue * DrawsPerBucket;

        for (int i = 0; i < draws; i++)
        {
            var value = random.Next(maxValue);
            Assert.InRange(value, 0, maxValue - 1);
            counts[value]++;
        }

        // Pearson's chi-square against the uniform expectation. The critical value is taken
        // generously (roughly the 0.999 quantile, approximated as df + 4*sqrt(2*df)) so the test
        // fails on a broken generator, not on an unlucky seed.
        var expected = (double)draws / maxValue;
        var chiSquare = counts.Sum(count => (count - expected) * (count - expected) / expected);

        var degreesOfFreedom = maxValue - 1;
        var critical = degreesOfFreedom + 4.0 * Math.Sqrt(2.0 * degreesOfFreedom);

        Assert.True(chiSquare < critical, $"chi-square {chiSquare:F2} exceeded {critical:F2}");
    }

    [Fact]
    public void NextRejectsANegativeBound()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new SeededRandomProvider(seed: 1).Next(-1));

    [Fact]
    public void RawOutputHasNoStuckBits()
    {
        // xoshiro256** fails this only if the state was seeded to something degenerate — the
        // all-zero state, most obviously, which never leaves itself.
        var random = new SeededRandomProvider(seed: 0);

        var orOfAll = 0UL;
        var andOfAll = ulong.MaxValue;
        for (int i = 0; i < 1_000; i++)
        {
            var value = random.NextULong();
            orOfAll |= value;
            andOfAll &= value;
        }

        Assert.Equal(ulong.MaxValue, orOfAll);
        Assert.Equal(0UL, andOfAll);
    }
}
