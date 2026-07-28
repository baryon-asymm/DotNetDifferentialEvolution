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
    /// Gets what the engine must provision for this strategy before it can build a trial vector.
    /// The builder validates the declaration and refuses to build a configuration that cannot
    /// satisfy it; the engine maintains the state a declared requirement implies.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="MutationRequirements.ControlParameters"/>, because reading F and CR
    /// from the <see cref="MutationContext"/> is the normal shape of a strategy and getting no
    /// provider is the failure that is worth catching at build time rather than discovering as a
    /// run that quietly optimizes nothing. A strategy that carries its own control parameters —
    /// like <see cref="MutationStrategy"/> — declares <see cref="MutationRequirements.None"/>.
    /// </remarks>
    public MutationRequirements Requirements => MutationRequirements.ControlParameters;

    /// <summary>
    /// Builds a trial individual (mutation + crossover) into
    /// <see cref="MutationContext.TrialIndividual"/>.
    /// </summary>
    /// <param name="context">The data required to build the trial vector.</param>
    public void Mutate(
        in MutationContext context);
}
