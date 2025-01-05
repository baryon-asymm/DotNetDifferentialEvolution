using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

public class SimpleSumEvaluator : ITestFitnessFunctionEvaluator
{
    public ReadOnlyMemory<double> LowerBounds { get; init; }
    
    public ReadOnlyMemory<double> UpperBounds { get; init; }
    
    public SimpleSumEvaluator(
        ReadOnlyMemory<double> lowerBounds,
        ReadOnlyMemory<double> upperBounds)
    {
        if (lowerBounds.Length != upperBounds.Length)
            throw new ArgumentException("Lower and upper bounds must have the same length.");
        
        LowerBounds = lowerBounds;
        UpperBounds = upperBounds;
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

    public ReadOnlyMemory<double> GetLowerBounds() => LowerBounds;

    public ReadOnlyMemory<double> GetUpperBounds() => UpperBounds;

    public double GetGlobalMinimumFfValue() => LowerBounds.ToArray().Sum();

    public ReadOnlyMemory<double> GetGlobalMinimumGenes() => LowerBounds;
}
