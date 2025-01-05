namespace DotNetDifferentialEvolution.Interfaces;

/// <summary>
/// Represents a mechanism for evaluating the fitness value of a set of genes 
/// in an optimization problem. The goal is to minimize the fitness value, 
/// where lower values indicate better solutions.
/// </summary>
public interface IFitnessFunctionEvaluator
{
    /// <summary>
    /// Evaluates the fitness value of a candidate solution represented by a set of genes.
    /// The fitness function determines the quality of the solution in the context of the optimization problem.
    /// </summary>
    /// <param name="genes">A read-only span of <see cref="double"/> values representing the genes 
    /// (parameters) of the candidate solution to be evaluated.</param>
    /// <returns>A <see cref="double"/> representing the computed fitness value of the given genes.
    /// Lower values indicate better solutions.</returns>
    public double Evaluate(
        ReadOnlySpan<double> genes);
}
