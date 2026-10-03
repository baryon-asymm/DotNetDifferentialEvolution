namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Styblinski-Tang function: <c>f(x) = ½ Σ (xᵢ⁴ − 16xᵢ² + 5xᵢ)</c>. Multimodal, with a
/// global minimum at <c>xᵢ ≈ −2.903534</c> and value <c>f* ≈ −39.16599·n</c> — a benchmark
/// whose optimum is a negative, dimension-scaled value rather than zero. Domain [-5, 5]ⁿ.
/// Validated on the fitness value.
/// </summary>
public sealed class StyblinskiTangEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>The per-dimension contribution to the (approximate) global minimum value.</summary>
    public const double MinimumValuePerDimension = -39.16599;

    /// <summary>The per-dimension global minimizer coordinate.</summary>
    public const double Minimizer = -2.903534;

    /// <summary>Evaluates the Styblinski-Tang function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        foreach (var gene in genes)
        {
            var g2 = gene * gene;
            sum += g2 * g2 - 16.0 * g2 + 5.0 * gene;
        }

        return 0.5 * sum;
    }

    /// <summary>Returns the lower bound of the Styblinski-Tang domain: -5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    /// <summary>Returns the upper bound of the Styblinski-Tang domain: 5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.0);

    /// <summary>Returns the approximate Styblinski-Tang global minimum value, <see cref="MinimumValuePerDimension"/> times <see cref="BenchmarkFunctionEvaluator.Dimension"/>.</summary>
    public override double GetGlobalMinimumFfValue() => MinimumValuePerDimension * Dimension;

    /// <summary>Returns the approximate Styblinski-Tang global minimizer, <see cref="Minimizer"/> in every dimension.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(Minimizer);
}
