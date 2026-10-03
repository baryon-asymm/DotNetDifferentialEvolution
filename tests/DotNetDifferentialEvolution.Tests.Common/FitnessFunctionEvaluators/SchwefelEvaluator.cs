namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Schwefel function: <c>f(x) = 418.9829·n − Σ xᵢ·sin(√|xᵢ|)</c>. Deceptive and multimodal —
/// the global minimum sits near a domain corner (<c>xᵢ ≈ 420.9687</c>), far from the
/// second-best minima, so greedy search is easily misled. Global minimum <c>f* ≈ 0</c>.
/// Domain [-500, 500]ⁿ. Validated on the fitness value (the minimizer is not reached exactly).
/// </summary>
public sealed class SchwefelEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>Evaluates the Schwefel function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        foreach (var gene in genes)
        {
            sum += gene * Math.Sin(Math.Sqrt(Math.Abs(gene)));
        }

        return 418.9829 * genes.Length - sum;
    }

    /// <summary>Returns the lower bound of the Schwefel domain: -500 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-500.0);

    /// <summary>Returns the upper bound of the Schwefel domain: 500 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(500.0);

    /// <summary>
    /// Returns 0, the declared Schwefel global minimum value. It is approximate: with the rounded
    /// constant 418.9829 the true minimum lies slightly above 0.
    /// </summary>
    public override double GetGlobalMinimumFfValue() => 0.0;
}
