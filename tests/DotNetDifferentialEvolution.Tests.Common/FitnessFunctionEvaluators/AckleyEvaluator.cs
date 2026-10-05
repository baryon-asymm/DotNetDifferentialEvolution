namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

/// <summary>
/// Ackley function: a nearly-flat outer region riddled with local minima and a single deep
/// global basin at the origin. Multimodal; tests an algorithm's ability to avoid being
/// trapped in the flat region. Global minimum <c>f* = 0</c> at the origin. Domain
/// [-32.768, 32.768]ⁿ.
/// </summary>
public sealed class AckleyEvaluator(
    int dimension = 2) : BenchmarkFunctionEvaluator(dimension)
{
    private const double A = 20.0;
    private const double B = 0.2;
    private const double C = 2.0 * Math.PI;

    /// <summary>Evaluates the Ackley function at <paramref name="genes"/>.</summary>
    /// <param name="genes">The point to evaluate, one value per dimension.</param>
    /// <returns>The function value at <paramref name="genes"/>.</returns>
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

    /// <summary>Returns the lower bound of the Ackley domain: -32.768 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetLowerBounds() => UniformBounds(-32.768);

    /// <summary>Returns the upper bound of the Ackley domain: 32.768 in every dimension.</summary>
    public override ReadOnlyMemory<double> GetUpperBounds() => UniformBounds(32.768);

    /// <summary>Returns 0, the Ackley global minimum value.</summary>
    public override double GetGlobalMinimumFfValue() => 0.0;

    /// <summary>Returns the Ackley global minimizer, the origin.</summary>
    public override ReadOnlyMemory<double> GetGlobalMinimumGenes() => UniformMinimizer(0.0);
}
