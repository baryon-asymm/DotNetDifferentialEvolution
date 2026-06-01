namespace DotNetDifferentialEvolution.MutationStrategies.Interfaces;

/// <summary>
/// Defines the interface for mutation strategies in Differential Evolution.
/// </summary>
public interface IMutationStrategy
{
    /// <summary>
    /// Builds a trial individual (mutation + crossover) into
    /// <see cref="MutationContext.TrialIndividual"/>.
    /// </summary>
    /// <param name="context">The data required to build the trial vector.</param>
    public void Mutate(
        in MutationContext context);
}
