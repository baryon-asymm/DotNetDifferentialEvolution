using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.Benchmark.Functions;

/// <summary>
/// The Ackley function — a multimodal benchmark with a nearly flat outer region and a
/// global minimum of 0 at the origin. Domain: [-32.768, 32.768]^n.
/// </summary>
public class AckleyEvaluator : IFitnessFunctionEvaluator
{
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var squaredSum = 0.0;
        var cosineSum = 0.0;
        foreach (var x in genes)
        {
            squaredSum += x * x;
            cosineSum += Math.Cos(2.0 * Math.PI * x);
        }

        var n = genes.Length;
        return -20.0 * Math.Exp(-0.2 * Math.Sqrt(squaredSum / n))
               - Math.Exp(cosineSum / n)
               + 20.0
               + Math.E;
    }

    public double Evaluate(
        int workerIndex,
        ReadOnlySpan<double> genes) => Evaluate(genes);
}
