using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.TerminationStrategies;

/// <summary>
/// Represents a termination strategy that limits the number of generations in Differential Evolution.
/// </summary>
public class LimitGenerationNumberTerminationStrategy : ITerminationStrategy
{
    /// <summary>
    /// Gets the maximum number of generations allowed.
    /// </summary>
    public int MaxGenerationNumber { get; init; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="LimitGenerationNumberTerminationStrategy"/> class.
    /// </summary>
    /// <param name="maxGenerationNumber">The maximum number of generations allowed.</param>
    public LimitGenerationNumberTerminationStrategy(
        int maxGenerationNumber)
    {
        MaxGenerationNumber = maxGenerationNumber;
    }
    
    /// <summary>
    /// Determines whether the evolution process should terminate based on the current population.
    /// </summary>
    /// <param name="population">The current population of individuals.</param>
    /// <returns><c>true</c> if the evolution process should terminate; otherwise, <c>false</c>.</returns>
    public bool ShouldTerminate(
        Population population)
    {
        return population.GenerationNumber >= MaxGenerationNumber;
    }
}
