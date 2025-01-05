namespace DotNetDifferentialEvolution.MutationStrategies.Interfaces;

/// <summary>
/// Defines the interface for mutation strategies in Differential Evolution.
/// </summary>
public interface IMutationStrategy
{
    /// <summary>
    /// Mutates an individual in the population.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to mutate.</param>
    /// <param name="population">The population of individuals.</param>
    /// <param name="trialIndividual">The trial individual to be mutated.</param>
    public void Mutate(
        int individualIndex,
        Span<double> population,
        Span<double> trialIndividual);
}
