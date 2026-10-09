using System.Diagnostics;
using DotNetDifferentialEvolution.GPU.Objectives;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// The arithmetic of check P4's pair, written once for the pointwise objective and its monolithic twin: D = 10, P = 50
/// points, each 40 rounds of <c>Exp</c> and <c>Pow</c> on doubles; the fitness is the sum of the points in point order.
/// </summary>
internal static class LatencyArithmetic
{
    /// <summary>P.</summary>
    public const int PointCount = 50;

    /// <summary>D.</summary>
    public const int GenomeSize = 10;

    private const int Rounds = 40;

    /// <summary>One point of one individual: 40 rounds of <c>Exp</c> and <c>Pow</c>.</summary>
    /// <param name="genes">The individual's genes.</param>
    /// <param name="point">The point.</param>
    /// <returns>The point's value.</returns>
    public static double Point(GeneView genes, int point)
    {
        var sum = 0.0;
        for (var round = 0; round < Rounds; round++)
        {
            var x = genes[(point + round) % genes.Length];
            sum += Math.Pow(1.0 + Math.Exp(-0.001 * x * x), 1.5);
        }

        return sum;
    }
}

/// <summary>Check P4's pointwise objective.</summary>
internal readonly struct LatencyPointwise : IGpuPointwiseFitnessFunction<double>
{
    /// <inheritdoc />
    public double EvaluatePoint(GeneView genes, int point) => LatencyArithmetic.Point(genes, point);

    /// <inheritdoc />
    public double Combine(GeneView genes, PointView<double> points)
    {
        var total = 0.0;
        for (var p = 0; p < points.Length; p++)
        {
            total += points[p];
        }

        return total;
    }
}

/// <summary>Check P4's monolithic twin: one thread computes all 50 points.</summary>
internal readonly struct LatencyMonolithic : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var total = 0.0;
        for (var p = 0; p < LatencyArithmetic.PointCount; p++)
        {
            total += LatencyArithmetic.Point(genes, p);
        }

        return total;
    }
}

/// <summary>Records the time of each call and takes nothing else from the snapshot.</summary>
internal sealed class StopwatchObserver : IGpuPopulationUpdatedHandler
{
    private readonly List<long> _timestamps = [];

    /// <summary>Gets the <see cref="Stopwatch.GetTimestamp"/> at each call, in order.</summary>
    public IReadOnlyList<long> Timestamps => _timestamps;

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot) => _timestamps.Add(Stopwatch.GetTimestamp());
}

/// <summary>
/// Check P4 of the Kernels node's ACCEPTANCE.md, the point of the pointwise objective, latency. An objective of P = 50 points,
/// each 40 rounds of <c>Exp</c> and <c>Pow</c> on doubles, under DE/rand/1/bin on CUDA: per generation, the median of three
/// timed batches of 50 generations after one warm-up batch, at N = 1 024 and N = 16 384, pointwise against its monolithic
/// twin. A batch is the time between two calls of an observer due every 50 generations, each of which has waited for the
/// device to finish the batch (the observer downloads the population: the cost of one download per batch is in every
/// figure). Pass: at N = 1 024 the pointwise generation is at least 4× faster. The four figures are printed.
/// </summary>
/// <param name="output">Receives the four figures.</param>
public class PointwiseLatencyTests(ITestOutputHelper output)
{
    private const int BatchGenerations = 50;
    private const int TimedBatches = 3;
    private const double RequiredSpeedup = 4.0;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, LatencyArithmetic.GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, LatencyArithmetic.GenomeSize)];

    /// <summary>P4: per generation at N = 1 024 and N = 16 384, pointwise against monolithic; at N = 1 024 at least 4× faster.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public async Task ThePointwiseGenerationIsAtLeastFourTimesFasterAtOneThousandAndTwentyFourIndividuals()
    {
        var (smallMonolithic, smallPointwise) = await Measure(1024).ConfigureAwait(true);
        var (largeMonolithic, largePointwise) = await Measure(16_384).ConfigureAwait(true);

        output.WriteLine($"N = 1024: monolithic {smallMonolithic * 1e3:F4} ms per generation, pointwise {smallPointwise * 1e3:F4} ms, {smallMonolithic / smallPointwise:F2}x");
        output.WriteLine($"N = 16384: monolithic {largeMonolithic * 1e3:F4} ms per generation, pointwise {largePointwise * 1e3:F4} ms, {largeMonolithic / largePointwise:F2}x");

        Assert.True(
            smallMonolithic >= RequiredSpeedup * smallPointwise,
            $"at N = 1024 the pointwise generation takes {smallPointwise * 1e3:F4} ms and the monolithic {smallMonolithic * 1e3:F4} ms: " +
            $"{smallMonolithic / smallPointwise:F2}x, below {RequiredSpeedup}x");
    }

    private static async Task<(double Monolithic, double Pointwise)> Measure(int populationSize)
    {
        var monolithic = await PerGeneration(
            GpuDifferentialEvolutionBuilder.ForFunction(default(LatencyMonolithic)), populationSize).ConfigureAwait(true);
        var pointwise = await PerGeneration(
            GpuDifferentialEvolutionBuilder.ForPointwiseFunction<LatencyPointwise, double>(default, LatencyArithmetic.PointCount),
            populationSize).ConfigureAwait(true);
        return (monolithic, pointwise);
    }

    /// <summary>The median, over the timed batches, of the seconds one generation takes.</summary>
    private static async Task<double> PerGeneration<TFunction>(IGpuBoundsRequired<TFunction> start, int populationSize)
        where TFunction : struct
    {
        var observer = new StopwatchObserver();
        using var optimizer = start
            .WithBounds(Lower, Upper)
            .WithPopulationSize(populationSize)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(BatchGenerations * (1 + TimedBatches))
            .OnDevice(GpuDevice.Cuda)
            .WithSeed(1)
            .WithPopulationUpdateHandler(observer, BatchGenerations)
            .Build();
        var begin = Stopwatch.GetTimestamp();
        _ = await optimizer.RunAsync().ConfigureAwait(true);

        // The first interval, from the start to the first call, is the warm-up batch.
        Assert.Equal(1 + TimedBatches, observer.Timestamps.Count);
        var marks = new[] { begin }.Concat(observer.Timestamps).ToArray();
        var batches = Enumerable.Range(1, TimedBatches)
            .Select(k => Stopwatch.GetElapsedTime(marks[k], marks[k + 1]).TotalSeconds / BatchGenerations)
            .Order()
            .ToArray();
        return batches[TimedBatches / 2];
    }
}
