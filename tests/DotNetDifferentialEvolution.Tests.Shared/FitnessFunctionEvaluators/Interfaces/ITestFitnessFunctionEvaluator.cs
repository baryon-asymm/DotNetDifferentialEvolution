using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

public interface ITestFitnessFunctionEvaluator : IFitnessFunctionEvaluator
{
    public ReadOnlyMemory<double> GetLowerBounds();
    
    public ReadOnlyMemory<double> GetUpperBounds();
    
    public double GetGlobalMinimumFfValue();
    
    public ReadOnlyMemory<double> GetGlobalMinimumGenes();
}
