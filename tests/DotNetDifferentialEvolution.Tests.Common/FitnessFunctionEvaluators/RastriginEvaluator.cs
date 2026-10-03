namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Rastrigin function: <c>f(x) = 10n + Σ [xᵢ² − 10 cos(2π xᵢ)]</c>. Highly multimodal and
/// separable, with a regular grid of local minima — a classic exploration test. Global
/// minimum <c>f* = 0</c> at the origin. Domain [-5.12, 5.12]ⁿ.
/// </summary>
public sealed class RastriginEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>Evaluates the Rastrigin function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 10.0 * genes.Length;
        foreach (var gene in genes)
        {
            sum += gene * gene - 10.0 * Math.Cos(2.0 * Math.PI * gene);
        }

        return sum;
    }

    /// <summary>Returns the lower bound of the Rastrigin domain: -5.12 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.12);

    /// <summary>Returns the upper bound of the Rastrigin domain: 5.12 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.12);

    /// <summary>Returns 0, the Rastrigin global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Rastrigin global minimizer, the origin.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
