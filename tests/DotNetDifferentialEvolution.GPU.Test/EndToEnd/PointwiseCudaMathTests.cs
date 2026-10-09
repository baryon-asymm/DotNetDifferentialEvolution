using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Objectives;
using ILGPU;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>The result of one point of the data-and-math objective (check P3): two doubles and an int.</summary>
/// <param name="Damping"><c>exp(−s²/20)</c>.</param>
/// <param name="Growth"><c>(1 + s²)^0.37</c>.</param>
/// <param name="Positive">1 when the point's argument is positive, else 0.</param>
internal readonly record struct MathPoint(double Damping, double Growth, int Positive);

/// <summary>
/// The arithmetic of check P3's pair, written once for the pointwise objective and its monolithic twin. D = 10, P = 50. The
/// point's argument <c>s</c> is the genes weighted by the objective's own <c>ArrayView</c>; its result uses <c>Exp</c> and
/// <c>Pow</c>, which on CUDA are completed by the libdevice post-link; the fitness folds the 50 results in point order.
/// </summary>
internal static class MathArithmetic
{
    /// <summary>P.</summary>
    public const int PointCount = 50;

    /// <summary>D.</summary>
    public const int GenomeSize = 10;

    /// <summary>The result of one point of one individual.</summary>
    /// <param name="weights">The objective's data, <c>P·D</c> weights on the accelerator.</param>
    /// <param name="genes">The individual's genes.</param>
    /// <param name="point">The point.</param>
    /// <returns>The result.</returns>
    public static MathPoint Point(ArrayView<double> weights, GeneView genes, int point)
    {
        var argument = 0.0;
        for (var j = 0; j < genes.Length; j++)
        {
            argument += weights[point * genes.Length + j] * genes[j];
        }

        var square = argument * argument;
        return new MathPoint(Math.Exp(-square / 20.0), Math.Pow(1.0 + square, 0.37), argument > 0.0 ? 1 : 0);
    }

    /// <summary>Adds one point's result to the running fitness.</summary>
    /// <param name="total">The fitness so far.</param>
    /// <param name="result">The next point's result.</param>
    /// <returns>The new running fitness.</returns>
    public static double Fold(double total, MathPoint result) => total + result.Growth / (1.0 + result.Damping) + result.Positive;

    /// <summary>The weights: fixed, small, of both signs.</summary>
    /// <returns>The <c>P·D</c> weights.</returns>
    public static double[] Weights() =>
        [.. Enumerable.Range(0, PointCount * GenomeSize).Select(k => (k * 37 % 23 - 11) * 0.05)];
}

/// <summary>Check P3's pointwise objective: an <c>ArrayView</c> field, <c>Exp</c> and <c>Pow</c> in <see cref="EvaluatePoint"/>.</summary>
/// <param name="weights">The weights, allocated on the accelerator the optimizer runs on.</param>
internal readonly struct MathPointwise(ArrayView<double> weights) : IGpuPointwiseFitnessFunction<MathPoint>
{
    /// <inheritdoc />
    public MathPoint EvaluatePoint(GeneView genes, int point) => MathArithmetic.Point(weights, genes, point);

    /// <inheritdoc />
    public double Combine(GeneView genes, PointView<MathPoint> points)
    {
        var total = 0.0;
        for (var p = 0; p < points.Length; p++)
        {
            total = MathArithmetic.Fold(total, points[p]);
        }

        return total;
    }
}

/// <summary>Check P3's monolithic twin: the same arithmetic in one thread.</summary>
/// <param name="weights">The weights, allocated on the accelerator the optimizer runs on.</param>
internal readonly struct MathMonolithic(ArrayView<double> weights) : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var total = 0.0;
        for (var p = 0; p < MathArithmetic.PointCount; p++)
        {
            total = MathArithmetic.Fold(total, MathArithmetic.Point(weights, genes, p));
        }

        return total;
    }
}

/// <summary>
/// Check P3 of the Kernels node's ACCEPTANCE.md: a pointwise objective that carries an <c>ArrayView&lt;double&gt;</c> field
/// and calls <c>Exp</c> and <c>Pow</c> in <c>EvaluatePoint</c>, with a result struct of two <c>double</c>s and an
/// <c>int</c>, builds and runs on CUDA and equals its monolithic twin bit for bit (L-SHADE, N_init 1 024, P = 50, seed 1,
/// 50 generations). The CPU accelerator runs the same pair in CI, as a control of the data and the result struct.
/// </summary>
/// <param name="output">Receives the figures of each run.</param>
public class PointwiseCudaMathTests(ITestOutputHelper output)
{
    private const int PopulationSize = 1024;
    private const int Generations = 50;
    private const long Budget = 100_000;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, MathArithmetic.GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, MathArithmetic.GenomeSize)];

    /// <summary>P3 on CUDA.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public Task DataAndMathEqualTheMonolithicTwinOnCuda() => AssertTwins(Backend.Cuda);

    /// <summary>The same pair on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task DataAndMathEqualTheMonolithicTwinOnTheCpuAccelerator() => AssertTwins(Backend.Cpu);

    private static async Task<RunRecord> Run<TFunction>(IGpuBoundsRequired<TFunction> start, Accelerator accelerator)
        where TFunction : struct
    {
        var hasher = new SnapshotHasher();
        using var optimizer = start
            .WithBounds(Lower, Upper)
            .WithPopulationSize(PopulationSize)
            .WithLShade(Budget)
            .WithGenerationLimit(Generations)
            .OnAccelerator(accelerator)
            .WithSeed(1)
            .WithPopulationUpdateHandler(hasher, 10)
            .Build();
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        return new RunRecord(
            result.Generations,
            result.EvaluationCount,
            BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue),
            [.. result.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits)],
            hasher.Hashes);
    }

    private async Task AssertTwins(Backend backend)
    {
        using var lease = DeviceSelector.Open(backend);
        var accelerator = lease.Accelerator;
        using var binding = accelerator.BindScoped();
        using var weights = accelerator.Allocate1D(MathArithmetic.Weights());

        var twin = await Run(GpuDifferentialEvolutionBuilder.ForFunction(new MathMonolithic(weights.View)), accelerator).ConfigureAwait(true);
        var pointwise = await Run(
            GpuDifferentialEvolutionBuilder.ForPointwiseFunction<MathPointwise, MathPoint>(new MathPointwise(weights.View), MathArithmetic.PointCount),
            accelerator).ConfigureAwait(true);

        output.WriteLine(
            $"{backend}: {BitConverter.Int64BitsToDouble(pointwise.FitnessBits):R} after {pointwise.Generations} generations, " +
            $"{pointwise.Evaluations} evaluations; the monolithic twin {BitConverter.Int64BitsToDouble(twin.FitnessBits):R}");
        Assert.Equal(Generations, pointwise.Generations);
        Assert.True(double.IsFinite(BitConverter.Int64BitsToDouble(pointwise.FitnessBits)), "the fitness is not finite");
        RunRecord.AssertSame(twin, pointwise, backend.ToString());
    }
}
