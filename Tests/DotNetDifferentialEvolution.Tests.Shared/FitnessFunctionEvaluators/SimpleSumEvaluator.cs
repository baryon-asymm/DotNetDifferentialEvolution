using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

public class SimpleSumEvaluator : IFitnessFunctionEvaluator
{
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;

        foreach (var gene in genes)
            sum += gene;

        return sum;
    }
}
