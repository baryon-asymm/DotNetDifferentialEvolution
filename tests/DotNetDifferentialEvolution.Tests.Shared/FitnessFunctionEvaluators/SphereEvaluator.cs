namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Sphere function: <c>f(x) = Σ xᵢ²</c>. Unimodal, separable, convex — the easiest sanity
/// check. Global minimum <c>f* = 0</c> at the origin. Domain [-5.12, 5.12]ⁿ.
/// </summary>
public sealed class SphereEvaluator : BenchmarkFunctionEvaluator
{
    public SphereEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        foreach (var gene in genes)
            sum += gene * gene;

        return sum;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.12);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.12);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
