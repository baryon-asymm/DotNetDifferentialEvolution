using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// A named Differential Evolution variant: the extension point behind
/// <see cref="DifferentialEvolutionBuilder.WithVariant"/>. jDE, JADE, SHADE and L-SHADE are
/// implemented as instances of it, so a variant defined outside this library is configured and
/// validated by exactly the same path as a built-in one rather than being assembled piecemeal
/// from the individual <c>With…</c> calls and hoping the pieces match.
/// </summary>
public interface IDeVariant
{
    /// <summary>
    /// Builds the variant's strategies for the given problem dimensions. Called once, when the
    /// variant is selected on the builder.
    /// </summary>
    /// <param name="configuration">The problem dimensions known at configuration time.</param>
    /// <returns>The strategies to install.</returns>
    public DeVariantSetup Configure(
        in DeVariantConfiguration configuration);

    /// <summary>
    /// Checks the variant against the rest of the configuration, once it is complete. Called from
    /// <see cref="DifferentialEvolutionBuilder.Build"/>, after the builder's own checks; throw
    /// <see cref="InvalidOperationException"/> to reject the configuration.
    /// </summary>
    /// <param name="configuration">The problem dimensions the variant was configured with.</param>
    /// <param name="terminationStrategy">The configured termination condition. L-SHADE checks its
    /// evaluation budget against this, because a population schedule driven by a budget the run
    /// will not actually consume never reaches its minimum.</param>
    /// <remarks>
    /// Defaults to accepting everything, so a variant with nothing to cross-check implements only
    /// <see cref="Configure"/>.
    /// </remarks>
    public void Validate(
        in DeVariantConfiguration configuration,
        ITerminationStrategy terminationStrategy)
    {
    }
}
