using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

/// <summary>
/// Defines the interface for termination strategies in Differential Evolution.
/// </summary>
public interface ITerminationStrategy
{
    /// <summary>
    /// Determines whether the evolution process should terminate based on the current population.
    /// </summary>
    /// <param name="population">The current population of individuals.</param>
    /// <returns><c>true</c> if the evolution process should terminate; otherwise, <c>false</c>.</returns>
    public bool ShouldTerminate(Population population);
}
