using System.Globalization;
using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.RandomProviders;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, the <b>Gpu</b> halves of checks A3 and A4, on the owner's devices.
/// <list type="bullet">
/// <item>A3: an instance with a ranking plan and N_init = 8 192 calibrates its ranking limit to 4 096 on the RTX 5070 Ti
/// (CUDA) and to 1 024 on the gfx1036 (OpenCL); the times the calibration measured are printed.</item>
/// <item>A4, CUDA, device time from a context opened with profiling (<c>DeviceSelector.OpenForTiming</c>), the median of 20
/// calls after 2 warm-up calls, in microseconds: the best index at N = 1 024 takes at most 25 µs; at N = 46 080 the wide
/// chunk c(N) is not slower than chunks of 1 024 nor than chunks of 32, both forced through the test seam in the same
/// process; all three are printed.</item>
/// </list>
/// </summary>
/// <param name="output">The test output the times are printed to.</param>
[Trait("Category", "Gpu")]
public class BookkeepingTimingTests(ITestOutputHelper output)
{
    private const int WarmUpCalls = 2;
    private const int TimedCalls = 20;
    private const int CalibratedCount = 8192;
    private const int BestCount = 1024;
    private const int LargeBestCount = 46_080;
    private const double BestIndexLimit = 25.0;
    private const int CaseSeed = 20261021;

    /// <summary>A3: on the RTX 5070 Ti the calibration finds counting not slower up to 4 096 and slower at 8 192.</summary>
    [Fact]
    public void OnCudaTheRankingLimitIsCalibratedToFourThousandAndNinetySix() =>
        AssertCalibratedLimit(Backend.Cuda, 4096);

    /// <summary>A3: on the gfx1036 the calibration finds counting slower already at 2 048.</summary>
    [Fact]
    public void OnOpenClTheRankingLimitIsCalibratedToOneThousandAndTwentyFour() =>
        AssertCalibratedLimit(Backend.OpenCL, 1024);

    /// <summary>A4: the best index at N = 1 024 takes at most 25 µs of device time per call.</summary>
    [Fact]
    public void TheBestIndexAtOneThousandAndTwentyFourTakesAtMostTwentyFiveMicroseconds()
    {
        using var lease = DeviceSelector.OpenForTiming(Backend.Cuda);

        var microseconds = BestIndexTime(lease.Accelerator, BestCount, new BookkeepingTuning());

        output.WriteLine($"{lease.Accelerator.Name}: best index at N = {BestCount}: {microseconds:F1} us of device time per call");
        Assert.True(microseconds <= BestIndexLimit, $"the best index at N = {BestCount} takes {microseconds:F1} us per call, above {BestIndexLimit:F0} us");
    }

    /// <summary>A4: at N = 46 080 the wide chunk c(N) is not slower than chunks of 1 024 nor than chunks of 32.</summary>
    [Fact]
    public void TheWideChunkOfTheSquareRootIsNotSlowerThanEitherExtreme()
    {
        using var lease = DeviceSelector.OpenForTiming(Backend.Cuda);
        var accelerator = lease.Accelerator;

        var chosen = BestIndexTime(accelerator, LargeBestCount, new BookkeepingTuning());
        var chunkOfThousand = BestIndexTime(accelerator, LargeBestCount, new BookkeepingTuning(WideChunkSize: BookkeepingKernels.ChunkSize));
        var chunkOfWarp = BestIndexTime(accelerator, LargeBestCount, new BookkeepingTuning(WideChunkSize: 32));

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{accelerator.Name}: best index at N = {LargeBestCount}, device time per call: c(N) = {BookkeepingKernels.WideChunkSizeOf(LargeBestCount)}: {chosen:F1} us; chunks of {BookkeepingKernels.ChunkSize}: {chunkOfThousand:F1} us; chunks of 32: {chunkOfWarp:F1} us"));
        Assert.True(chosen <= chunkOfThousand, $"c(N) {chosen:F1} us is slower than chunks of {BookkeepingKernels.ChunkSize} {chunkOfThousand:F1} us at N = {LargeBestCount}");
        Assert.True(chosen <= chunkOfWarp, $"c(N) {chosen:F1} us is slower than chunks of 32 {chunkOfWarp:F1} us at N = {LargeBestCount}");
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

    private void AssertCalibratedLimit(Backend backend, int expected)
    {
        using var lease = DeviceSelector.Open(backend);
        var accelerator = lease.Accelerator;
        var plan = new BookkeepingPlan(CalibratedCount, 1, SchemeKind.CurrentToPBest, ParameterRule.Fixed, 0, 0, 0.0, false, double.NaN, double.NaN, null);

        using var bookkeeping = new GenerationBookkeeping(accelerator, plan, seed: 1);

        foreach (var measurement in bookkeeping.RankingMeasurements)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{accelerator.Name}: ranking at n = {measurement.Count}: counting {measurement.Times.Counting:F1} us, bitonic network {measurement.Times.Bitonic:F1} us"));
        }

        output.WriteLine($"{accelerator.Name}: N_init = {CalibratedCount}, ranking limit L = {bookkeeping.RankingLimit}");
        Assert.Equal(expected, bookkeeping.RankingLimit);
    }

    /// <summary>The median device time of the best index of <paramref name="count"/> values under <paramref name="tuning"/>, in microseconds.</summary>
    private double BestIndexTime(Accelerator accelerator, int count, BookkeepingTuning tuning)
    {
        var plan = new BookkeepingPlan(count, 1, SchemeKind.Best, ParameterRule.Fixed, 0, 0, 0.0, false, double.NaN, double.NaN, null);
        using var bookkeeping = new GenerationBookkeeping(accelerator, plan, seed: 1, tuning);
        using var fitness = accelerator.Allocate1D<double>(count);
        fitness.View.CopyFromCPU(Values(count));
        output.WriteLine($"{accelerator.Name}: N = {count}, wide chunk {bookkeeping.WideChunkSize}");

        return DeviceMedian(accelerator, () => bookkeeping.FindBest(fitness.View, count));
    }

    /// <summary>The median, in microseconds, of the device time between two profiling markers around <paramref name="call"/>, after two warm-up calls.</summary>
    private static double DeviceMedian(Accelerator accelerator, Action call)
    {
        var stream = accelerator.DefaultStream;
        for (var warmUp = 0; warmUp < WarmUpCalls; warmUp++)
        {
            call();
            accelerator.Synchronize();
        }

        var times = new double[TimedCalls];
        for (var timed = 0; timed < TimedCalls; timed++)
        {
            var start = stream.AddProfilingMarker();
            call();
            var end = stream.AddProfilingMarker();
            end.Synchronize();
            times[timed] = end.MeasureFrom(start).TotalMicroseconds;
        }

        Array.Sort(times);
        return (times[TimedCalls / 2 - 1] + times[TimedCalls / 2]) / 2.0;
    }
}
