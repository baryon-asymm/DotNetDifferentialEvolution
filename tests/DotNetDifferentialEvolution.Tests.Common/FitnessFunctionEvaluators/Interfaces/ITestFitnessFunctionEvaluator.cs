namespace DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators.Interfaces;

/// <summary>
/// A fitness function used in tests: besides evaluating, it declares its search domain and its
/// known global minimum, so a test can build a problem from it and check what the optimizer found.
/// </summary>
public interface ITestFitnessFunctionEvaluator : IFitnessFunctionEvaluator
{
    /// <summary>Returns the lower bound of each gene of the search domain.</summary>
    /// <returns>One bound per gene.</returns>
    ReadOnlyMemory<double> GetLowerBounds();

    /// <summary>Returns the upper bound of each gene of the search domain.</summary>
    /// <returns>One bound per gene.</returns>
    ReadOnlyMemory<double> GetUpperBounds();

    /// <summary>Returns the fitness value of the global minimum.</summary>
    /// <returns>The lowest value the function takes within its domain.</returns>
    double GetGlobalMinimumFfValue();

    /// <summary>Returns the genes at which the global minimum is reached.</summary>
    /// <returns>The global minimizer, one value per gene.</returns>
    ReadOnlyMemory<double> GetGlobalMinimumGenes();
}
