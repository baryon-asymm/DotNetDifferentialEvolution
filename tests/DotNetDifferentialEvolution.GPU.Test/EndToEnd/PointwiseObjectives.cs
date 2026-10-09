using System.Security.Cryptography;
using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>The result of one point of the pair objective (check P1): a residual and a flag.</summary>
/// <param name="Residual">The squared residual of the point.</param>
/// <param name="Flag">0 when the residual exceeds <see cref="PairArithmetic.Threshold"/>, else 1.</param>
internal readonly record struct PairPoint(double Residual, int Flag);

/// <summary>Where <see cref="PairArithmetic.Reduce{TPoints}"/> gets the result of a point: computed on the spot, or read from the stored results.</summary>
internal interface IPairPoints
{
    /// <summary>The result of one point.</summary>
    /// <param name="point">The point, <c>0 ≤ point &lt; P</c>.</param>
    /// <returns>The result.</returns>
    PairPoint At(int point);
}

/// <summary>The points computed in the thread that reduces them: the monolithic twin.</summary>
/// <param name="genes">The individual's genes.</param>
internal readonly struct ComputedPoints(GeneView genes) : IPairPoints
{
    /// <inheritdoc />
    public PairPoint At(int point) => PairArithmetic.Point(genes, point);
}

/// <summary>The points read from the results the package stored: the pointwise objective.</summary>
/// <param name="points">The individual's stored results.</param>
internal readonly struct StoredPoints(PointView<PairPoint> points) : IPairPoints
{
    /// <inheritdoc />
    public PairPoint At(int point) => points[point];
}

/// <summary>
/// The arithmetic of check P1's pair, written once: both the pointwise objective and its monolithic twin call these two
/// functions, so that they perform the same operations in the same order by construction. D = 10; P = 12 points in
/// 3 groups of 4. Point p is the squared residual of the fixed linear form <c>Σ_j a_pj·x_j − b_p</c> and a flag that is 0
/// when the residual exceeds <see cref="Threshold"/>; the fitness is, group by group in point order, the square root of the
/// group's mean residual, summed over the groups, plus 1 per point whose flag is set.
/// </summary>
internal static class PairArithmetic
{
    /// <summary>D.</summary>
    public const int GenomeSize = 10;

    /// <summary>P.</summary>
    public const int PointCount = 12;

    /// <summary>The residual above which a point's flag is 0.</summary>
    public const double Threshold = 4.0;

    private const int GroupSize = 4;

    /// <summary>The result of one point of one individual.</summary>
    /// <param name="genes">The individual's genes.</param>
    /// <param name="point">The point.</param>
    /// <returns>The residual and the flag.</returns>
    public static PairPoint Point(GeneView genes, int point)
    {
        var sum = 0.0;
        for (var j = 0; j < genes.Length; j++)
        {
            sum += Coefficient(point, j) * genes[j];
        }

        var difference = sum - Target(point);
        var residual = difference * difference;
        return new PairPoint(residual, residual > Threshold ? 0 : 1);
    }

    /// <summary>The fitness from the results of the 12 points, read in point order.</summary>
    /// <typeparam name="TPoints">Where the results come from.</typeparam>
    /// <param name="points">The results.</param>
    /// <returns>The fitness.</returns>
    public static double Reduce<TPoints>(TPoints points)
        where TPoints : struct, IPairPoints
    {
        var total = 0.0;
        var flagged = 0;
        for (var group = 0; group < PointCount / GroupSize; group++)
        {
            var sum = 0.0;
            for (var k = 0; k < GroupSize; k++)
            {
                var result = points.At(group * GroupSize + k);
                sum += result.Residual;
                flagged += result.Flag;
            }

            total += Math.Sqrt(sum / GroupSize);
        }

        return total + flagged;
    }

    private static double Coefficient(int point, int gene) => ((point * 7 + gene * 3) % 11 - 5) * 0.25;

    private static double Target(int point) => (point * 5 % 7 - 3) * 0.5;
}

/// <summary>Check P1's pointwise objective.</summary>
internal readonly struct PairPointwise : IGpuPointwiseFitnessFunction<PairPoint>
{
    /// <inheritdoc />
    public PairPoint EvaluatePoint(GeneView genes, int point) => PairArithmetic.Point(genes, point);

    /// <inheritdoc />
    public double Combine(GeneView genes, PointView<PairPoint> points) => PairArithmetic.Reduce(new StoredPoints(points));
}

/// <summary>Check P1's monolithic twin: the same arithmetic in one thread.</summary>
internal readonly struct PairMonolithic : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes) => PairArithmetic.Reduce(new ComputedPoints(genes));
}

/// <summary>The pair's fitness cut to its integer part, so that the best value soon stops moving: the pointwise objective.</summary>
internal readonly struct SteppedPairPointwise : IGpuPointwiseFitnessFunction<PairPoint>
{
    /// <inheritdoc />
    public PairPoint EvaluatePoint(GeneView genes, int point) => PairArithmetic.Point(genes, point);

    /// <inheritdoc />
    public double Combine(GeneView genes, PointView<PairPoint> points) => Math.Floor(PairArithmetic.Reduce(new StoredPoints(points)));
}

/// <summary>The stepped pair's monolithic twin.</summary>
internal readonly struct SteppedPairMonolithic : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes) => Math.Floor(PairArithmetic.Reduce(new ComputedPoints(genes)));
}

/// <summary>Everything a run is compared by: the result's bits and one hash per population snapshot.</summary>
/// <param name="Generations">The generations run.</param>
/// <param name="Evaluations">The evaluations.</param>
/// <param name="FitnessBits">The best fitness as IEEE-754 bits.</param>
/// <param name="GeneBits">The best genes as IEEE-754 bits.</param>
/// <param name="SnapshotHashes">The SHA-256 of each snapshot the observer received, in order.</param>
internal sealed record RunRecord(int Generations, long Evaluations, long FitnessBits, long[] GeneBits, IReadOnlyList<string> SnapshotHashes)
{
    /// <summary>Asserts that two runs are the same run bit for bit.</summary>
    /// <param name="expected">The reference run.</param>
    /// <param name="actual">The run compared with it.</param>
    /// <param name="what">What is compared, for the messages.</param>
    public static void AssertSame(RunRecord expected, RunRecord actual, string what)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        Assert.True(expected.Generations == actual.Generations, $"{what}: generations {expected.Generations} and {actual.Generations}");
        Assert.True(expected.Evaluations == actual.Evaluations, $"{what}: evaluations {expected.Evaluations} and {actual.Evaluations}");
        Assert.True(expected.FitnessBits == actual.FitnessBits, $"{what}: fitness bits {expected.FitnessBits:X16} and {actual.FitnessBits:X16}");
        Assert.Equal(expected.GeneBits, actual.GeneBits);
        Assert.True(
            expected.SnapshotHashes.Count == actual.SnapshotHashes.Count,
            $"{what}: {expected.SnapshotHashes.Count} snapshots and {actual.SnapshotHashes.Count}");
        for (var k = 0; k < expected.SnapshotHashes.Count; k++)
        {
            Assert.True(expected.SnapshotHashes[k] == actual.SnapshotHashes[k], $"{what}: snapshot {k} differs");
        }
    }
}

/// <summary>Takes the SHA-256 of each snapshot: its generation, evaluation count, population size, and the bits of its genes and fitness values.</summary>
internal sealed class SnapshotHasher : IGpuPopulationUpdatedHandler
{
    private readonly List<string> _hashes = [];

    /// <summary>Gets the hash of each snapshot received, in order.</summary>
    public IReadOnlyList<string> Hashes => _hashes;

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, snapshot.Generation);
        Append(hash, snapshot.EvaluationCount);
        Append(hash, snapshot.PopulationSize);
        foreach (var gene in snapshot.Genes.ToArray())
        {
            Append(hash, BitConverter.DoubleToInt64Bits(gene));
        }

        foreach (var fitness in snapshot.FitnessFunctionValues.ToArray())
        {
            Append(hash, BitConverter.DoubleToInt64Bits(fitness));
        }

        _hashes.Add(Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static void Append(IncrementalHash hash, long value) => hash.AppendData(BitConverter.GetBytes(value));
}
