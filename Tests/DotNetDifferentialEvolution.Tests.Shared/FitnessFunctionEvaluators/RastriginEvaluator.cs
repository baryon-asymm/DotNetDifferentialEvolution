namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Rastrigin function: <c>f(x) = 10n + Σ [xᵢ² − 10 cos(2π xᵢ)]</c>. Highly multimodal and
/// separable, with a regular grid of local minima — a classic exploration test. Global
/// minimum <c>f* = 0</c> at the origin. Domain [-5.12, 5.12]ⁿ.
/// </summary>
public sealed class RastriginEvaluator : BenchmarkFunctionEvaluator
{
    public RastriginEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 10.0 * genes.Length;
        foreach (var gene in genes)
            sum += gene * gene - 10.0 * Math.Cos(2.0 * Math.PI * gene);

        return sum;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.12);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.12);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
