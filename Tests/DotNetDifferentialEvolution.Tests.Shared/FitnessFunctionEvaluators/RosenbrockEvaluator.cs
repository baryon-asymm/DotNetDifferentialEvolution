using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

public class RosenbrockEvaluator : ITestFitnessFunctionEvaluator
{
    public const double A = 1.0;
    public const double B = 100.0;
    
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];
        
        return Math.Pow(A - x, 2) + B * Math.Pow(y - x * x, 2);
    }

    public ReadOnlyMemory<double> GetLowerBounds()
    {
        return new[] { -5.0, -5.0 };
    }

    public ReadOnlyMemory<double> GetUpperBounds()
    {
        return new[] { 5.0, 5.0 };
    }

    public double GetGlobalMinimumFfValue()
    {
        return 0.0;
    }

    public ReadOnlyMemory<double> GetGlobalMinimumGenes()
    {
        return new[] { A, A * A };
    }
}
