namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Dixon-Price function: <c>f(x) = (x₁ − 1)² + Σ_{i=2}^{n} i·(2xᵢ² − x_{i−1})²</c>.
/// Valley-shaped and non-separable, with a global minimizer at the non-trivial point
/// <c>xᵢ = 2^(−(2ⁱ−2)/2ⁱ)</c> rather than the origin — a good check that the optimizer is
/// not implicitly biased toward zero. Global minimum <c>f* = 0</c>. Domain [-10, 10]ⁿ.
/// Validated on the fitness value.
/// </summary>
public sealed class DixonPriceEvaluator : BenchmarkFunctionEvaluator
{
    public DixonPriceEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x0 = genes[0] - 1.0;
        var result = x0 * x0;

        for (int i = 1; i < genes.Length; i++)
        {
            var term = 2.0 * genes[i] * genes[i] - genes[i - 1];
            result += (i + 1) * term * term;
        }

        return result;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-10.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(10.0);

    public override double GetGlobalMinimumFfValue() => 0.0;
}
