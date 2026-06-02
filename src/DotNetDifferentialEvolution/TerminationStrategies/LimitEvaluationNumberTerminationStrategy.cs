using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

/// <summary>
/// Terminates the evolution once a budget of fitness-function evaluations has been reached.
/// This is the natural stopping criterion for L-SHADE, whose population-size reduction is
/// driven by the same evaluation budget.
/// </summary>
public class LimitEvaluationNumberTerminationStrategy : ITerminationStrategy
{
    /// <summary>
    /// Gets the maximum number of fitness-function evaluations allowed.
    /// </summary>
    public long MaxEvaluationNumber { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LimitEvaluationNumberTerminationStrategy"/> class.
    /// </summary>
    /// <param name="maxEvaluationNumber">The maximum number of fitness-function evaluations allowed.</param>
    public LimitEvaluationNumberTerminationStrategy(
        long maxEvaluationNumber)
    {
        MaxEvaluationNumber = maxEvaluationNumber;
    }

    /// <inheritdoc />
    public bool ShouldTerminate(
        Population population)
    {
        ArgumentNullException.ThrowIfNull(population);

        return population.EvaluationCount >= MaxEvaluationNumber;
    }
}
