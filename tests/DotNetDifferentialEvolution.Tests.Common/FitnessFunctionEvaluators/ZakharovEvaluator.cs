namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Zakharov function: <c>f(x) = Σ xᵢ² + (Σ 0.5·i·xᵢ)² + (Σ 0.5·i·xᵢ)⁴</c> (i = 1…n).
/// Unimodal but non-separable (a plate-shaped landscape), so it stresses the algorithm's
/// handling of coupled variables. Global minimum <c>f* = 0</c> at the origin.
/// Domain [-5, 10]ⁿ.
/// </summary>
public sealed class ZakharovEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>Evaluates the Zakharov function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sumSquares = 0.0;
        var sumHalf = 0.0;
        for (var i = 0; i < genes.Length; i++)
        {
            sumSquares += genes[i] * genes[i];
            sumHalf += 0.5 * (i + 1) * genes[i];
        }

        var sumHalf2 = sumHalf * sumHalf;
        return sumSquares + sumHalf2 + sumHalf2 * sumHalf2;
    }

    /// <summary>Returns the lower bound of the Zakharov domain: -5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    /// <summary>Returns the upper bound of the Zakharov domain: 10 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(10.0);

    /// <summary>Returns 0, the Zakharov global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Zakharov global minimizer, the origin.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
