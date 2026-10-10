using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Test.Kernels;
using DotNetDifferentialEvolution.RandomProviders;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, check A4's wide chunk (CI half), on ILGPU's CPU accelerator.
/// <c>WideChunkSizeOf</c> gives 32 for N_init 1 and 1 024, 64 for 1 025, 128 for 16 384 and 224 for 46 080 (and the
/// neighbours, by hand); an instance fixes <c>WideChunkSize</c> at construction (an L-SHADE instance of 1 025 keeps 64 when
/// it finds the best of 1 024, and its result is <c>BestPick</c>'s); and the best index does not depend on the chunk, whatever
/// the test forces. The device-time halves are <c>BookkeepingTimingTests</c>, under <c>Gpu</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class WideChunkTests : IDisposable
{
    private const int CaseSeed = 20261030;
    private const double Lowest = -1e300;

    private readonly HostStep _step = new();

    /// <inheritdoc />
    public void Dispose() => _step.Dispose();

    /// <summary>A4: the wide chunk is the ceiling square root rounded up to a multiple of 32, at least 32.</summary>
    /// <param name="populationSize">N_init.</param>
    /// <param name="expected">The wide chunk.</param>
    [Theory]
    [InlineData(1, 32)]
    [InlineData(2, 32)]
    [InlineData(1024, 32)]
    [InlineData(1025, 64)]
    [InlineData(4096, 64)]
    [InlineData(4097, 96)]
    [InlineData(16_384, 128)]
    [InlineData(46_080, 224)]
    [InlineData(100_000, 320)]
    [InlineData(int.MaxValue, 46_368)]
    public void TheWideChunkIsTheCeilingRootRoundedUpToAWarp(int populationSize, int expected) =>
        Assert.Equal(expected, BookkeepingKernels.WideChunkSizeOf(populationSize));

    /// <summary>A population below 1 has no wide chunk.</summary>
    /// <param name="populationSize">N_init.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnEmptyPopulationIsRefused(int populationSize) =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BookkeepingKernels.WideChunkSizeOf(populationSize));

    /// <summary>The wide chunk count is ⌈N / c⌉, and 0 for no individual.</summary>
    [Fact]
    public void TheWideChunkCountRoundsUp()
    {
        Assert.Equal(0, BookkeepingKernels.WideChunkCount(0, 32));
        Assert.Equal(1, BookkeepingKernels.WideChunkCount(1, 32));
        Assert.Equal(1, BookkeepingKernels.WideChunkCount(32, 32));
        Assert.Equal(2, BookkeepingKernels.WideChunkCount(33, 32));
        Assert.Equal(45, BookkeepingKernels.WideChunkCount(46_080, 1024));
        Assert.Equal(206, BookkeepingKernels.WideChunkCount(46_080, 224));
        Assert.Equal(46_314, BookkeepingKernels.WideChunkCount(int.MaxValue, 46_368));
    }

    /// <summary>A4: an instance fixes its wide chunk from N_init; an L-SHADE instance of 1 025 finds the best of 1 024 with chunks of 64, as <c>BestPick</c> does.</summary>
    [Fact]
    public void AnInstanceKeepsTheWideChunkOfItsInitialPopulation()
    {
        using var bookkeeping = Bookkeeping(1025, tuning: null);
        Assert.Equal(64, bookkeeping.WideChunkSize);

        var random = new SeededRandomProvider(CaseSeed);
        for (var array = 0; array < 40; array++)
        {
            var values = Values(random, 1024);
            if (array % 2 == 0)
            {
                // The lowest value just before and after a boundary of 64, and in a later chunk.
                values[63] = values[64] = values[127] = Lowest;
            }

            Assert.Equal(BestPick.IndexOf(values), Best(bookkeeping, values));
        }

        Assert.Equal(64, bookkeeping.WideChunkSize);
    }

    /// <summary>A4: the best index is <c>BestPick</c>'s whatever wide chunk a test forces, ties across its boundaries and all-NaN arrays included.</summary>
    /// <param name="wideChunkSize">The forced wide chunk.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(224)]
    [InlineData(1024)]
    [InlineData(5000)]
    public void TheBestIndexDoesNotDependOnTheWideChunk(int wideChunkSize)
    {
        const int count = 5000;
        using var bookkeeping = Bookkeeping(count, new BookkeepingTuning(WideChunkSize: wideChunkSize));
        Assert.Equal(wideChunkSize, bookkeeping.WideChunkSize);

        var random = new SeededRandomProvider(CaseSeed + wideChunkSize);
        for (var array = 0; array < 30; array++)
        {
            var size = array % 3 == 0 ? count : 1 + random.Next(count);
            var values = array % 10 == 9 ? [.. Enumerable.Repeat(double.NaN, size)] : Values(random, size);
            if (array % 2 == 0 && size > 2 * wideChunkSize)
            {
                // The same lowest value just before and after a boundary of the forced chunk, and in a later chunk.
                values[wideChunkSize - 1] = values[wideChunkSize] = values[2 * wideChunkSize - 1] = Lowest;
            }

            Assert.True(BestPick.IndexOf(values) == Best(bookkeeping, values), $"wide chunk {wideChunkSize}, array {array}, N {size}");
        }
    }

    /// <summary>A forced chunk below 1 is refused.</summary>
    [Fact]
    public void AForcedChunkBelowOneIsRefused() =>
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Bookkeeping(100, new BookkeepingTuning(WideChunkSize: 0)));

    private static double[] Values(SeededRandomProvider random, int count)
    {
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = random.Next(4) == 0 ? Math.Floor(random.NextDouble() * 4.0) : 100.0 * random.NextDouble() - 50.0;
        }

        return values;
    }

    private GenerationBookkeeping Bookkeeping(int populationSize, BookkeepingTuning? tuning)
    {
        // L-SHADE-shaped, whose population shrinks: the best index is found for sizes below N_init.
        var plan = new BookkeepingPlan(populationSize, 1, SchemeKind.Best, ParameterRule.Fixed, 0, 0, 0.0, true, double.NaN, double.NaN, null);
        return new GenerationBookkeeping(_step.Accelerator, plan, seed: 1, tuning ?? new BookkeepingTuning());
    }

    private int Best(GenerationBookkeeping bookkeeping, double[] values)
    {
        using var fitness = _step.Accelerator.Allocate1D<double>(values.Length);
        fitness.View.CopyFromCPU(values);
        bookkeeping.FindBest(fitness.View, values.Length);
        _step.Accelerator.Synchronize();
        var best = new int[1];
        bookkeeping.Views.BestIndex.CopyToCPU(best);
        return best[0];
    }
}
