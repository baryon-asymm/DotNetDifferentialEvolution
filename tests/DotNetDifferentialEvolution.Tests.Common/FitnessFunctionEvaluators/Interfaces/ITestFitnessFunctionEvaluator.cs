namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators.Interfaces;

public interface ITestFitnessFunctionEvaluator : IFitnessFunctionEvaluator
{
    ReadOnlyMemory<double> GetLowerBounds();

    ReadOnlyMemory<double> GetUpperBounds();

    double GetGlobalMinimumFfValue();

    ReadOnlyMemory<double> GetGlobalMinimumGenes();
}
