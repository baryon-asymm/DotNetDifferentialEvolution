namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Griewank function: <c>f(x) = Σ xᵢ²/4000 − Π cos(xᵢ/√i) + 1</c> (i = 1…n). Multimodal
/// with many widespread local minima created by the product term. Global minimum
/// <c>f* = 0</c> at the origin. Domain [-600, 600]ⁿ.
/// </summary>
public sealed class GriewankEvaluator : BenchmarkFunctionEvaluator
{
    public GriewankEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        var product = 1.0;
        for (int i = 0; i < genes.Length; i++)
        {
            sum += genes[i] * genes[i] / 4000.0;
            product *= Math.Cos(genes[i] / Math.Sqrt(i + 1));
        }

        return sum - product + 1.0;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-600.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(600.0);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
