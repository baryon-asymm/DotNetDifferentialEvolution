using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Test.Kernels;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.RandomProviders;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, checks S9 and S10, on ILGPU's CPU accelerator.
/// <list type="bullet">
/// <item>S9: for 200 random arrays, the bitonic network, and ranking by counting where N is at most the instance's limit
/// (2 048 on the CPU accelerator, which is not timed), each give exactly the order by (key, index),
/// <see cref="double.NaN"/> as +∞. N: 150 arrays in [1, limit], 42 above it up to 20 000, and 1, 2,
/// 2 048, 2 049, 8 192, 8 193, 16 384, 20 000; a limit forced on the instance decides the ranking too. Values: <see cref="double.NaN"/>, ±∞, ±0 and ties from a small set, mixed with random
/// ones. Every tenth array has distinct keys and is also held to the CPU package's
/// <see cref="PopulationSortHelper"/>.</item>
/// <item>S10: the best-index kernels equal <see cref="BestPick.IndexOf"/> on 200 random arrays, N in [1, 5 000], with ties
/// placed across the 32 and the 1 024 boundaries and arrays of <see cref="double.NaN"/> only.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public sealed class RankingTests : IDisposable
{
    private const int ArrayCount = 200;
    private const int LargestRanked = 20000;
    private const int LargestPicked = 5000;
    private const int CaseSeed = 20261010;

    private static readonly int[] EdgeSizes = [1, 2, 2048, 2049, 8192, 8193, 16384, 20000];
    private static readonly double[] Specials = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, -0.0, 1.0, -1.0, 2.0];

    private readonly HostStep _step = new();

    /// <inheritdoc />
    public void Dispose() => _step.Dispose();

    /// <summary>S9: both rankings give the order by (key, index); distinct keys give the CPU package's order.</summary>
    [Fact]
    public void BothRankingsGiveTheOrderByKeyThenIndex()
    {
        var random = new SeededRandomProvider(CaseSeed);
        using var bookkeeping = Bookkeeping(LargestRanked, SchemeKind.CurrentToPBest, shrinks: true);
        using var fitness = _step.Accelerator.Allocate1D<double>(LargestRanked);

        // The CPU accelerator is not timed (A3): the instance's limit is the untimed one, which the edge sizes straddle.
        var limit = bookkeeping.RankingLimit;
        Assert.Equal(RankingCalibration.UntimedLimit, limit);
        Assert.Empty(bookkeeping.RankingMeasurements);
        var counted = 0;
        for (var array = 0; array < ArrayCount; array++)
        {
            var count = array < EdgeSizes.Length
                ? EdgeSizes[array]
                : array < EdgeSizes.Length + 150
                    ? 1 + random.Next(limit)
                    : limit + 1 + random.Next(LargestRanked - limit);
            var distinct = array % 10 == 0;
            var values = Values(random, count, distinct);
            fitness.View.SubView(0, count).CopyFromCPU(values);
            var expected = Enumerable.Range(0, count).OrderBy(i => double.IsNaN(values[i]) ? double.PositiveInfinity : values[i]).ThenBy(i => i).ToArray();

            bookkeeping.RankByBitonicNetwork(fitness.View, count);
            Assert.Equal(expected, Ranking(bookkeeping, count));
            if (count <= limit)
            {
                bookkeeping.RankByCounting(fitness.View, count);
                Assert.Equal(expected, Ranking(bookkeeping, count));
                counted++;
            }

            if (distinct)
            {
                var cpu = new int[count];
                PopulationSortHelper.SortIndicesByFitness(cpu, values, count, new double[count]);
                Assert.Equal(cpu, expected);
            }
        }

        Assert.True(counted >= 150, $"ranked by counting {counted} times");
    }

    /// <summary>A3: a limit forced on the instance times nothing and decides which ranking runs; both give the order by (key, index).</summary>
    [Fact]
    public void AForcedLimitDecidesTheRankingAndTimesNothing()
    {
        const int forced = 100;
        var random = new SeededRandomProvider(CaseSeed + 2);
        using var bookkeeping = Bookkeeping(1000, SchemeKind.CurrentToPBest, shrinks: true, new BookkeepingTuning(RankingLimit: forced));
        using var fitness = _step.Accelerator.Allocate1D<double>(1000);
        Assert.Equal(forced, bookkeeping.RankingLimit);
        Assert.Empty(bookkeeping.RankingMeasurements);

        foreach (var count in new[] { 1, forced - 1, forced, forced + 1, 500, 1000 })
        {
            var values = Values(random, count, distinct: false);
            fitness.View.SubView(0, count).CopyFromCPU(values);
            var expected = Enumerable.Range(0, count).OrderBy(i => double.IsNaN(values[i]) ? double.PositiveInfinity : values[i]).ThenBy(i => i).ToArray();

            bookkeeping.Rank(fitness.View, count);

            Assert.Equal(expected, Ranking(bookkeeping, count));
        }
    }

    /// <summary>A forced limit below 1 is refused.</summary>
    [Fact]
    public void AForcedLimitBelowOneIsRefused() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Bookkeeping(100, SchemeKind.CurrentToPBest, shrinks: false, new BookkeepingTuning(RankingLimit: 0)));

    /// <summary>S10: the best index is <see cref="BestPick.IndexOf"/>'s.</summary>
    [Fact]
    public void TheBestIndexIsBestPicks()
    {
        var random = new SeededRandomProvider(CaseSeed + 1);
        using var bookkeeping = Bookkeeping(LargestPicked, SchemeKind.Best);
        using var fitness = _step.Accelerator.Allocate1D<double>(LargestPicked);
        var best = new int[1];
        for (var array = 0; array < ArrayCount; array++)
        {
            var count = 1 + random.Next(LargestPicked);
            var values = array % 20 == 0 ? [.. Enumerable.Repeat(double.NaN, count)] : Values(random, count, distinct: false);
            if (array % 7 == 0 && count > 2048)
            {
                // The same lowest value just before and after a chunk boundary, and in a later chunk.
                values[1023] = values[1024] = values[2048] = -1e300;
            }

            if (array % 3 == 0 && count > 96)
            {
                // The same, for chunks of 32, the smallest the order-independent passes run in.
                values[31] = values[32] = values[95] = -1e300;
            }

            var wide = bookkeeping.WideChunkSize;
            if (array % 5 == 0 && count > 3 * wide)
            {
                // The same, for the instance's own wide chunk, c(N_init) (A4).
                values[wide - 1] = values[wide] = values[2 * wide - 1] = -1e300;
            }

            fitness.View.SubView(0, count).CopyFromCPU(values);
            bookkeeping.FindBest(fitness.View, count);
            _step.Accelerator.Synchronize();
            bookkeeping.Views.BestIndex.CopyToCPU(best);

            Assert.True(BestPick.IndexOf(values) == best[0], $"array {array}, N {count}: BestPick {BestPick.IndexOf(values)}, kernels {best[0]}");
        }
    }

    private static double[] Values(SeededRandomProvider random, int count, bool distinct)
    {
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = distinct
                ? i + random.NextDouble()
                : random.Next(4) == 0 ? Specials[random.Next(Specials.Length)] : 100.0 * random.NextDouble() - 50.0;
        }

        if (distinct)
        {
            // Shuffle, so that the ranking is not the identity.
            for (var i = count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        return values;
    }

    /// <summary>A bookkeeping for <paramref name="populationSize"/>; L-SHADE's, whose population shrinks, loads both rankings (A10).</summary>
    private GenerationBookkeeping Bookkeeping(int populationSize, SchemeKind scheme, bool shrinks = false, BookkeepingTuning? tuning = null) =>
        new(
            _step.Accelerator,
            new BookkeepingPlan(populationSize, 1, scheme, ParameterRule.Fixed, 0, 0, 0.0, shrinks, double.NaN, double.NaN, null),
            seed: 1,
            tuning ?? new BookkeepingTuning());

    private int[] Ranking(GenerationBookkeeping bookkeeping, int count)
    {
        _step.Accelerator.Synchronize();
        var ranking = new int[count];
        bookkeeping.Views.Ranking.SubView(0, count).CopyToCPU(ranking);
        return ranking;
    }
}
