using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

/// <summary>
/// Represents a termination strategy that limits the number of generations in Differential Evolution.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LimitGenerationNumberTerminationStrategy"/> class.
/// </remarks>
/// <param name="maxGenerationNumber">The maximum number of generations allowed.</param>
public class LimitGenerationNumberTerminationStrategy(
    int maxGenerationNumber) : ITerminationStrategy
{
    /// <summary>
    /// Gets the maximum number of generations allowed.
    /// </summary>
    public int MaxGenerationNumber { get; init; } = maxGenerationNumber;

    /// <summary>
    /// Determines whether the evolution process should terminate based on the current population.
    /// </summary>
    /// <param name="population">The current population of individuals.</param>
    /// <returns><c>true</c> if the evolution process should terminate; otherwise, <c>false</c>.</returns>
    public bool ShouldTerminate(
        Population population)
    {
        ArgumentNullException.ThrowIfNull(population);

        return population.GenerationNumber >= MaxGenerationNumber;
    }
}
