namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Sphere function: <c>f(x) = Σ xᵢ²</c>. Unimodal, separable, convex — the easiest sanity
/// check. Global minimum <c>f* = 0</c> at the origin. Domain [-5.12, 5.12]ⁿ.
/// </summary>
public sealed class SphereEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>Evaluates the sphere function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        foreach (var gene in genes)
        {
            sum += gene * gene;
        }

        return sum;
    }

    /// <summary>Returns the lower bound of the sphere domain: -5.12 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.12);

    /// <summary>Returns the upper bound of the sphere domain: 5.12 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.12);

    /// <summary>Returns 0, the sphere global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the sphere global minimizer, the origin.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
