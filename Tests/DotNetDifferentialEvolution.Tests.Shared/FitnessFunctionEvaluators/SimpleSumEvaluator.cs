using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

public class SimpleSumEvaluator : ITestFitnessFunctionEvaluator
{
    private readonly ReadOnlyMemory<double> _lowerBounds;
    private readonly ReadOnlyMemory<double> _upperBounds;

    public SimpleSumEvaluator(
        ReadOnlyMemory<double> lowerBounds,
        ReadOnlyMemory<double> upperBounds)
    {
        if (lowerBounds.Length != upperBounds.Length)
            throw new ArgumentException("Lower and upper bounds must have the same length.");

        _lowerBounds = lowerBounds;
        _upperBounds = upperBounds;
    }
    
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var sum = 0.0;

        foreach (var gene in genes)
            sum += gene;

        return sum;
    }

    public double Evaluate(
        int workerIndex,
        ReadOnlySpan<double> genes) => Evaluate(genes);

    public ReadOnlyMemory<double> GetLowerBounds() => _lowerBounds;

    public ReadOnlyMemory<double> GetUpperBounds() => _upperBounds;

    public double GetGlobalMinimumFfValue() => _lowerBounds.ToArray().Sum();

    public ReadOnlyMemory<double> GetGlobalMinimumGenes() => _lowerBounds;
}
