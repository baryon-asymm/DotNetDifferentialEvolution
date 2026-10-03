namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Sum of Different Powers function: <c>f(x) = Σ |xᵢ|^(i+1)</c> (i = 1…n). Unimodal; the
/// rising exponents make the landscape increasingly flat near the optimum, which probes
/// fine-grained convergence. Global minimum <c>f* = 0</c> at the origin. Domain [-1, 1]ⁿ.
/// </summary>
public sealed class SumOfDifferentPowersEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>Evaluates the Sum of Different Powers function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        for (var i = 0; i < genes.Length; i++)
        {
            sum += Math.Pow(Math.Abs(genes[i]), i + 2);
        }

        return sum;
    }

    /// <summary>Returns the lower bound of the Sum of Different Powers domain: -1 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-1.0);

    /// <summary>Returns the upper bound of the Sum of Different Powers domain: 1 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(1.0);

    /// <summary>Returns 0, the Sum of Different Powers global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Sum of Different Powers global minimizer, the origin.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
