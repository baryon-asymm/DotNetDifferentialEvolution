using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.Benchmark.Functions;

/// <summary>
/// The Rastrigin function — a highly multimodal benchmark with a global minimum of 0 at
/// the origin and many regularly spaced local minima. Domain: [-5.12, 5.12]^n.
/// </summary>
public class RastriginEvaluator : IFitnessFunctionEvaluator
{
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 10.0 * genes.Length;
        foreach (var x in genes)
            sum += x * x - 10.0 * Math.Cos(2.0 * Math.PI * x);

        return sum;
    }

    public double Evaluate(
        int workerIndex,
        ReadOnlySpan<double> genes) => Evaluate(genes);
}
