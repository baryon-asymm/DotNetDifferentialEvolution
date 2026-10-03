namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Rosenbrock function (a.k.a. the banana / valley function):
/// <c>f(x) = Σ_{i=0}^{n−2} [100·(x_{i+1} − xᵢ²)² + (1 − xᵢ)²]</c>. Unimodal but hard: the
/// minimum lies inside a long, narrow, curved valley. Non-separable. Global minimum
/// <c>f* = 0</c> at <c>(1, …, 1)</c>. Domain [-5, 5]ⁿ (defaults to the classic 2-D form).
/// </summary>
public class RosenbrockEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    /// <summary>
    /// The coefficient <c>a</c> of the classic 2-D form f = (a − x)² + b·(y − x²)². The n-D formula
    /// writes it as the literal 1, so it is retained for documentation.
    /// </summary>
    public const double A = 1.0;

    /// <summary>The coefficient <c>b</c> of the classic 2-D form, weighting the valley term in every dimension.</summary>
    public const double B = 100.0;

    /// <summary>Gets 2: the Rosenbrock function couples consecutive genes, so it needs at least two.</summary>
    protected override int MinimumDimension => 2;

    /// <summary>Evaluates the Rosenbrock function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        for (var i = 0; i < genes.Length - 1; i++)
        {
            var a = genes[i + 1] - genes[i] * genes[i];
            var b = 1.0 - genes[i];
            sum += B * a * a + b * b;
        }

        return sum;
    }

    /// <summary>Returns the lower bound of the Rosenbrock domain: -5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    /// <summary>Returns the upper bound of the Rosenbrock domain: 5 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.0);

    /// <summary>Returns 0, the Rosenbrock global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Rosenbrock global minimizer, <c>(1, …, 1)</c>.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(1.0);
}
