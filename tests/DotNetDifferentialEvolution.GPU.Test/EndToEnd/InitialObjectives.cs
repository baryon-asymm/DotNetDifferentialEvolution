using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>The squared deviation of one gene from <see cref="FiveSphere.Optimum"/>: the result of one point of the pointwise twin.</summary>
/// <param name="Squared">The squared deviation.</param>
internal readonly record struct SquaredDeviation(double Squared);

/// <summary>
/// Sphere with its optimum at <see cref="Optimum"/> in every gene, Σ (x_j − 5)², the objective of checks I2 to I7. The
/// pointwise twin has one point per gene and adds the points in gene order, the same operations in the same order.
/// </summary>
internal readonly struct FiveSphere : IGpuFitnessFunction
{
    /// <summary>The optimum of every gene.</summary>
    public const double Optimum = 5.0;

    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var sum = 0.0;
        for (var j = 0; j < genes.Length; j++)
        {
            var deviation = genes[j] - Optimum;
            sum += deviation * deviation;
        }

        return sum;
    }
}

/// <summary>The pointwise twin of <see cref="FiveSphere"/>: point <c>j</c> is gene <c>j</c>'s squared deviation.</summary>
internal readonly struct FiveSpherePointwise : IGpuPointwiseFitnessFunction<SquaredDeviation>
{
    /// <inheritdoc />
    public SquaredDeviation EvaluatePoint(GeneView genes, int point)
    {
        var deviation = genes[point] - FiveSphere.Optimum;
        return new SquaredDeviation(deviation * deviation);
    }

    /// <inheritdoc />
    public double Combine(GeneView genes, PointView<SquaredDeviation> points)
    {
        var sum = 0.0;
        for (var j = 0; j < points.Length; j++)
        {
            sum += points[j].Squared;
        }

        return sum;
    }
}
