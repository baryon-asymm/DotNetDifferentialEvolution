using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>Sphere, Σ x_j²: minimum 0 at the origin.</summary>
internal readonly struct Sphere : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var sum = 0.0;
        for (var j = 0; j < genes.Length; j++)
        {
            sum += genes[j] * genes[j];
        }

        return sum;
    }
}

/// <summary>Rosenbrock, Σ 100·(x_{j+1} − x_j²)² + (1 − x_j)²: minimum 0 at (1, …, 1).</summary>
internal readonly struct Rosenbrock : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var sum = 0.0;
        for (var j = 0; j + 1 < genes.Length; j++)
        {
            var valley = genes[j + 1] - genes[j] * genes[j];
            var offset = 1.0 - genes[j];
            sum += 100.0 * valley * valley + offset * offset;
        }

        return sum;
    }
}

/// <summary>
/// Rastrigin, 10·D + Σ (x_j² − 10·cos(2π·x_j)): minimum 0 at the origin, with a local minimum near
/// every integer point. The cosine is computed here (<see cref="CosTwoPi"/>) from the operations
/// an objective may use, since <c>Math.Cos</c> is not on the package's allow-list (invariant 8).
/// </summary>
internal readonly struct Rastrigin : IGpuFitnessFunction
{
    /// <summary>Terms of the cosine series: the first omitted one, y²⁸/28! at |y| ≤ π, is below 3e-16.</summary>
    private const int SeriesTerms = 14;

    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        var sum = 10.0 * genes.Length;
        for (var j = 0; j < genes.Length; j++)
        {
            sum += genes[j] * genes[j] - 10.0 * CosTwoPi(genes[j]);
        }

        return sum;
    }

    /// <summary>
    /// cos(2π·x): x reduced to t in [−1/2, 1/2] by the period, then the Taylor series of cos(2π·t)
    /// in Horner form. It is exactly 1 at every integer, so the minimum stays exactly 0.
    /// </summary>
    /// <param name="x">The argument, in periods.</param>
    /// <returns>The cosine.</returns>
    internal static double CosTwoPi(double x)
    {
        var t = x - Math.Floor(x + 0.5);
        var y = 2.0 * Math.PI * t;
        var z = y * y;
        var result = 1.0;
        for (var k = SeriesTerms; k >= 1; k--)
        {
            result = 1.0 - z / ((2.0 * k - 1.0) * (2.0 * k)) * result;
        }

        return result;
    }
}
