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
/// <item>S9: for 200 random arrays, the bitonic network, and ranking by counting where N ≤ 2 048, each give exactly the
/// order by (key, index), <see cref="double.NaN"/> as +∞. N: 150 arrays in [1, 2 048], 42 in [2 049, 20 000], and 1, 2,
/// 2 048, 2 049, 8 192, 8 193, 16 384, 20 000. Values: <see cref="double.NaN"/>, ±∞, ±0 and ties from a small set, mixed with random
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
        var counted = 0;
        for (var array = 0; array < ArrayCount; array++)
        {
            var count = array < EdgeSizes.Length
                ? EdgeSizes[array]
                : array < EdgeSizes.Length + 150
                    ? 1 + random.Next(GenerationBookkeeping.CountingRankLimit)
                    : GenerationBookkeeping.CountingRankLimit + 1 + random.Next(LargestRanked - GenerationBookkeeping.CountingRankLimit);
            var distinct = array % 10 == 0;
            var values = Values(random, count, distinct);
            fitness.View.SubView(0, count).CopyFromCPU(values);
            var expected = Enumerable.Range(0, count).OrderBy(i => double.IsNaN(values[i]) ? double.PositiveInfinity : values[i]).ThenBy(i => i).ToArray();

            bookkeeping.RankByBitonicNetwork(fitness.View, count);
            Assert.Equal(expected, Ranking(bookkeeping, count));
            if (count <= GenerationBookkeeping.CountingRankLimit)
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
                // The same, for the chunks of 32 the order-independent passes run in.
                values[31] = values[32] = values[95] = -1e300;
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
    private GenerationBookkeeping Bookkeeping(int populationSize, SchemeKind scheme, bool shrinks = false) =>
        new(
            _step.Accelerator,
            new BookkeepingPlan(populationSize, 1, scheme, ParameterRule.Fixed, 0, 0, 0.0, shrinks, double.NaN, double.NaN, null),
            seed: 1);

    private int[] Ranking(GenerationBookkeeping bookkeeping, int count)
    {
        _step.Accelerator.Synchronize();
        var ranking = new int[count];
        bookkeeping.Views.Ranking.SubView(0, count).CopyToCPU(ranking);
        return ranking;
    }
}
