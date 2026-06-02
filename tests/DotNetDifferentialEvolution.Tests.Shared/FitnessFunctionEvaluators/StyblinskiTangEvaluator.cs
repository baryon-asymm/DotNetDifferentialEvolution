namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Styblinski-Tang function: <c>f(x) = ½ Σ (xᵢ⁴ − 16xᵢ² + 5xᵢ)</c>. Multimodal, with a
/// global minimum at <c>xᵢ ≈ −2.903534</c> and value <c>f* ≈ −39.16599·n</c> — a benchmark
/// whose optimum is a negative, dimension-scaled value rather than zero. Domain [-5, 5]ⁿ.
/// Validated on the fitness value.
/// </summary>
public sealed class StyblinskiTangEvaluator : BenchmarkFunctionEvaluator
{
    /// <summary>The per-dimension contribution to the (approximate) global minimum value.</summary>
    public const double MinimumValuePerDimension = -39.16599;

    /// <summary>The per-dimension global minimizer coordinate.</summary>
    public const double Minimizer = -2.903534;

    public StyblinskiTangEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

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

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.0);

    public override double GetGlobalMinimumFfValue() => MinimumValuePerDimension * Dimension;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(Minimizer);
}
