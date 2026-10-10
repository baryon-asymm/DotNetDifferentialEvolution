using System.Diagnostics;
using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.RandomProviders;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, the <b>Gpu</b> halves of checks A3 and A4, on CUDA. Each time is the
/// median of 20 calls, each call timed from the host through the accelerator's synchronisation, after 2 warm-up calls, in
/// microseconds.
/// <list type="bullet">
/// <item>A3: ranking by counting at N = 2 048 is not slower than the bitonic network at 2 048; both are printed.</item>
/// <item>A4: the best index at N = 1 024 takes at most 25 µs per call; larger N are printed, not asserted.</item>
/// </list>
/// </summary>
/// <param name="output">The test output the times are printed to.</param>
[Trait("Category", "Gpu")]
public class BookkeepingTimingTests(ITestOutputHelper output)
{
    private const int WarmUpCalls = 2;
    private const int TimedCalls = 20;
    private const int RankedCount = 2048;
    private const int BestCount = 1024;
    private const double BestIndexLimit = 25.0;
    private const int CaseSeed = 20261021;

    /// <summary>A3: counting at N = 2 048 is not slower than the bitonic network at 2 048.</summary>
    [Fact]
    public void CountingIsNotSlowerThanTheBitonicNetworkAtTheLimit()
    {
        using var lease = DeviceSelector.Open(Backend.Cuda);
        var accelerator = lease.Accelerator;

        // An L-SHADE-shaped plan for twice the limit loads both rankings; the counting kernel is loaded for the limit.
        var plan = new BookkeepingPlan(2 * RankedCount, 1, SchemeKind.CurrentToPBest, ParameterRule.Fixed, 0, 0, 0.0, true, double.NaN, double.NaN, null);
        using var bookkeeping = new GenerationBookkeeping(accelerator, plan, seed: 1);
        using var fitness = accelerator.Allocate1D<double>(2 * RankedCount);
        fitness.View.SubView(0, RankedCount).CopyFromCPU(Values(RankedCount));

        var counting = Median(accelerator, () => bookkeeping.RankByCounting(fitness.View, RankedCount));
        var bitonic = Median(accelerator, () => bookkeeping.RankByBitonicNetwork(fitness.View, RankedCount));
        output.WriteLine($"{accelerator.Name}: ranking at N = {RankedCount}: counting {counting:F1} us per call, bitonic network {bitonic:F1} us per call");

        Assert.True(counting <= bitonic, $"counting {counting:F1} us is slower than the bitonic network {bitonic:F1} us at N = {RankedCount}");
    }

    /// <summary>A4: the best index at N = 1 024 takes at most 25 µs per call.</summary>
    [Fact]
    public void TheBestIndexAtOneThousandAndTwentyFourTakesAtMostTwentyFiveMicroseconds()
    {
        using var lease = DeviceSelector.Open(Backend.Cuda);
        var accelerator = lease.Accelerator;

        var measured = 0.0;
        foreach (var count in new[] { BestCount, 16384, 46080 })
        {
            var plan = new BookkeepingPlan(count, 1, SchemeKind.Best, ParameterRule.Fixed, 0, 0, 0.0, false, double.NaN, double.NaN, null);
            using var bookkeeping = new GenerationBookkeeping(accelerator, plan, seed: 1);
            using var fitness = accelerator.Allocate1D<double>(count);
            fitness.View.CopyFromCPU(Values(count));

            var microseconds = Median(accelerator, () => bookkeeping.FindBest(fitness.View, count));
            output.WriteLine($"{accelerator.Name}: best index at N = {count}: {microseconds:F1} us per call");
            if (count == BestCount)
            {
                measured = microseconds;
            }
        }

        Assert.True(measured <= BestIndexLimit, $"the best index at N = {BestCount} takes {measured:F1} us per call, above {BestIndexLimit:F0} us");
    }

    private static double[] Values(int count)
    {
        var random = new SeededRandomProvider(CaseSeed + count);
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = 100.0 * random.NextDouble() - 50.0;
        }

        return values;
    }

    /// <summary>The median, in microseconds, of the timed calls of <paramref name="call"/>, each followed by a synchronisation.</summary>
    private static double Median(Accelerator accelerator, Action call)
    {
        for (var warmUp = 0; warmUp < WarmUpCalls; warmUp++)
        {
            call();
            accelerator.Synchronize();
        }

        var times = new double[TimedCalls];
        for (var timed = 0; timed < TimedCalls; timed++)
        {
            var start = Stopwatch.GetTimestamp();
            call();
            accelerator.Synchronize();
            times[timed] = Stopwatch.GetElapsedTime(start).TotalMilliseconds * 1000.0;
        }

        Array.Sort(times);
        return (times[TimedCalls / 2 - 1] + times[TimedCalls / 2]) / 2.0;
    }
}
