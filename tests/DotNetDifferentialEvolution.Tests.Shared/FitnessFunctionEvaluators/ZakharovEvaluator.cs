namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Zakharov function: <c>f(x) = Σ xᵢ² + (Σ 0.5·i·xᵢ)² + (Σ 0.5·i·xᵢ)⁴</c> (i = 1…n).
/// Unimodal but non-separable (a plate-shaped landscape), so it stresses the algorithm's
/// handling of coupled variables. Global minimum <c>f* = 0</c> at the origin.
/// Domain [-5, 10]ⁿ.
/// </summary>
public sealed class ZakharovEvaluator : BenchmarkFunctionEvaluator
{
    public ZakharovEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sumSquares = 0.0;
        var sumHalf = 0.0;
        for (int i = 0; i < genes.Length; i++)
        {
            sumSquares += genes[i] * genes[i];
            sumHalf += 0.5 * (i + 1) * genes[i];
        }

        var sumHalf2 = sumHalf * sumHalf;
        return sumSquares + sumHalf2 + sumHalf2 * sumHalf2;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(10.0);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
