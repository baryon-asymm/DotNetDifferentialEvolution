namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

/// <summary>
/// Ackley function: a nearly-flat outer region riddled with local minima and a single deep
/// global basin at the origin. Multimodal; tests an algorithm's ability to avoid being
/// trapped in the flat region. Global minimum <c>f* = 0</c> at the origin. Domain
/// [-32.768, 32.768]ⁿ.
/// </summary>
public sealed class AckleyEvaluator : BenchmarkFunctionEvaluator
{
    private const double A = 20.0;
    private const double B = 0.2;
    private const double C = 2.0 * Math.PI;

    public AckleyEvaluator(
        int dimension = 2)
        : base(dimension)
    {
    }

    public override double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var n = genes.Length;
        var sumSquares = 0.0;
        var sumCos = 0.0;
        foreach (var gene in genes)
        {
            sumSquares += gene * gene;
            sumCos += Math.Cos(C * gene);
        }

        return -A * Math.Exp(-B * Math.Sqrt(sumSquares / n))
               - Math.Exp(sumCos / n)
               + A
               + Math.E;
    }

    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-32.768);

    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(32.768);

    public override double GetGlobalMinimumFfValue() => 0.0;

    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
