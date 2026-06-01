namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Schwefel function: <c>f(x) = 418.9829·n − Σ xᵢ·sin(√|xᵢ|)</c>. Deceptive and multimodal —
/// the global minimum sits near a domain corner (<c>xᵢ ≈ 420.9687</c>), far from the
/// second-best minima, so greedy search is easily misled. Global minimum <c>f* ≈ 0</c>.
/// Domain [-500, 500]ⁿ. Validated on the fitness value (the minimizer is not reached exactly).
/// </summary>
public sealed class SchwefelEvaluator : BenchmarkFunctionEvaluator
{
    public SchwefelEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        foreach (var gene in genes)
            sum += gene * Math.Sin(Math.Sqrt(Math.Abs(gene)));

        return 418.9829 * genes.Length - sum;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-500.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(500.0);

    public override double GetGlobalMinimumFfValue() => 0.0;
}
