namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Rosenbrock function (a.k.a. the banana / valley function):
/// <c>f(x) = Σ_{i=0}^{n−2} [100·(x_{i+1} − xᵢ²)² + (1 − xᵢ)²]</c>. Unimodal but hard: the
/// minimum lies inside a long, narrow, curved valley. Non-separable. Global minimum
/// <c>f* = 0</c> at <c>(1, …, 1)</c>. Domain [-5, 5]ⁿ (defaults to the classic 2-D form).
/// </summary>
public class RosenbrockEvaluator : BenchmarkFunctionEvaluator
{
    /// <summary>The classic 2-D coefficients, retained for documentation: f = (A − x)² + B·(y − x²)².</summary>
    public const double A = 1.0;
    public const double B = 100.0;

    public RosenbrockEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    protected override int MinimumDimension => 2;

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        for (int i = 0; i < genes.Length - 1; i++)
        {
            var a = genes[i + 1] - genes[i] * genes[i];
            var b = 1.0 - genes[i];
            sum += B * a * a + b * b;
        }

        return sum;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-5.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(5.0);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(1.0);
}
