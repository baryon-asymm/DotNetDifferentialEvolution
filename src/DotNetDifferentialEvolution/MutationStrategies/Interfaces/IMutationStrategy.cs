namespace DotNetDifferentialEvolution.MutationStrategies.Interfaces;

/// <summary>
/// Defines the interface for mutation strategies in Differential Evolution.
/// </summary>
public interface IMutationStrategy
{
    /// <summary>
    /// Gets the smallest population size for which this strategy can draw the distinct
    /// individuals it needs. The builder validates the configured population size against
    /// this value to avoid an unsatisfiable distinct-index search.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>2</c>; built-in strategies override it with the number of distinct
    /// individuals they require (e.g. <c>DE/rand/2</c> needs six).
    /// </remarks>
    public int MinimumPopulationSize => 2;

    /// <summary>
    /// Builds a trial individual (mutation + crossover) into
    /// <see cref="MutationContext.TrialIndividual"/>.
    /// </summary>
    /// <param name="context">The data required to build the trial vector.</param>
    public void Mutate(
        in MutationContext context);
}
