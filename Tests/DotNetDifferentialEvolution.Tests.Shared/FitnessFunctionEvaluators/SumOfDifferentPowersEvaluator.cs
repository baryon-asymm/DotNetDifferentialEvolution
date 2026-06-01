namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Sum of Different Powers function: <c>f(x) = Σ |xᵢ|^(i+1)</c> (i = 1…n). Unimodal; the
/// rising exponents make the landscape increasingly flat near the optimum, which probes
/// fine-grained convergence. Global minimum <c>f* = 0</c> at the origin. Domain [-1, 1]ⁿ.
/// </summary>
public sealed class SumOfDifferentPowersEvaluator : BenchmarkFunctionEvaluator
{
    public SumOfDifferentPowersEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;
        for (int i = 0; i < genes.Length; i++)
            sum += Math.Pow(Math.Abs(genes[i]), i + 2);

        return sum;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-1.0);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(1.0);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
