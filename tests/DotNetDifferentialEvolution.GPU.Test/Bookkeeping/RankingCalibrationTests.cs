using DotNetDifferentialEvolution.GPU.Bookkeeping;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, check A3's limit (CI half): <c>RankingCalibration.LimitOf</c> against a
/// scripted <c>time</c> that records the n it was asked. N_init at most 1 024 gives 1 024 and never asks; counting faster
/// at 2 048 and 4 096 and slower at 8 192 gives 4 096; slower at 2 048 gives 1 024; faster everywhere with N_init 3 000
/// gives 3 000 (asked at 2 048 and 3 000 only); faster everywhere with N_init 20 000 gives 8 192; and the first n at which
/// counting is slower ends the search, whatever it is at the larger ones. The device halves (4 096 on the RTX 5070 Ti, 1 024
/// on the gfx1036) are <c>BookkeepingTimingTests</c>, under <c>Gpu</c>.
/// </summary>
[Trait("Category", "Unit")]
public class RankingCalibrationTests
{
    private static readonly RankingTimes Faster = new(1.0, 2.0);
    private static readonly RankingTimes Equal = new(2.0, 2.0);
    private static readonly RankingTimes Slower = new(3.0, 2.0);

    /// <summary>A population of at most 1 024 gives 1 024 and the time is never asked.</summary>
    /// <param name="populationSize">N_init.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(1024)]
    public void AtMostTheFloorGivesTheFloorWithoutTiming(int populationSize)
    {
        var (limit, asked) = Run(populationSize, _ => Faster);

        Assert.Equal(1024, limit);
        Assert.Empty(asked);
    }

    /// <summary>Counting faster at 2 048 and 4 096 and slower at 8 192: 4 096.</summary>
    [Fact]
    public void TheLastSizeWhereCountingIsNotSlowerIsTheLimit()
    {
        var (limit, asked) = Run(20_000, n => n < 8192 ? Faster : Slower);

        Assert.Equal(4096, limit);
        Assert.Equal([2048, 4096, 8192], asked);
    }

    /// <summary>Counting slower at the first size: 1 024, and nothing larger is asked.</summary>
    [Fact]
    public void SlowerAtTheFirstSizeGivesTheFloor()
    {
        var (limit, asked) = Run(20_000, _ => Slower);

        Assert.Equal(1024, limit);
        Assert.Equal([2048], asked);
    }

    /// <summary>A population between the floor and 2 048 is timed at its own size: faster gives N_init, slower the floor.</summary>
    /// <param name="faster">Whether counting is faster at N_init.</param>
    /// <param name="expected">The limit.</param>
    [Theory]
    [InlineData(true, 1500)]
    [InlineData(false, 1024)]
    public void APopulationBelowTheFirstSizeIsTimedAtItsOwnSize(bool faster, int expected)
    {
        var (limit, asked) = Run(1500, _ => faster ? Faster : Slower);

        Assert.Equal(expected, limit);
        Assert.Equal([1500], asked);
    }

    /// <summary>Faster everywhere with N_init 3 000: 3 000, asked at 2 048 and 3 000 only.</summary>
    [Fact]
    public void SizesAreCappedAtThePopulationAndAskedOnce()
    {
        var (limit, asked) = Run(3000, _ => Faster);

        Assert.Equal(3000, limit);
        Assert.Equal([2048, 3000], asked);
    }

    /// <summary>Faster everywhere with N_init 20 000: the ceiling, 8 192.</summary>
    [Fact]
    public void NothingAboveTheCeilingIsTimed()
    {
        var (limit, asked) = Run(20_000, _ => Faster);

        Assert.Equal(8192, limit);
        Assert.Equal([2048, 4096, 8192], asked);
    }

    /// <summary>Equal times are not slower: counting keeps the size.</summary>
    [Fact]
    public void EqualTimesCountAsNotSlower()
    {
        var (limit, _) = Run(20_000, n => n < 8192 ? Equal : Slower);

        Assert.Equal(4096, limit);
    }

    /// <summary>The search stops at the first size where counting is slower, whatever it is at the larger ones.</summary>
    [Fact]
    public void TheSearchStopsAtTheFirstSlowerSize()
    {
        var (limit, asked) = Run(20_000, n => n == 4096 ? Slower : Faster);

        Assert.Equal(2048, limit);
        Assert.Equal([2048, 4096], asked);
    }

    /// <summary>A time that is not a number is not "not slower".</summary>
    [Fact]
    public void ATimeThatIsNotANumberEndsTheSearch()
    {
        var (limit, _) = Run(20_000, n => n == 4096 ? new RankingTimes(double.NaN, 2.0) : Faster);

        Assert.Equal(2048, limit);
    }

    private static (int Limit, List<int> Asked) Run(int populationSize, Func<int, RankingTimes> script)
    {
        var asked = new List<int>();
        var limit = RankingCalibration.LimitOf(populationSize, n =>
        {
            asked.Add(n);
            return script(n);
        });
        return (limit, asked);
    }
}
