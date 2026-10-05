using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// jDE (Brest et al., 2006): <c>DE/rand/1/bin</c> with per-individual self-adapting F and CR and
/// greedy selection. Each individual carries its own control parameters, which are re-sampled
/// with small probability each generation and inherited by a successful trial.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JdeVariant"/> class.
/// </remarks>
/// <param name="initialMutationForce">The initial mutation factor for every individual.</param>
/// <param name="initialCrossoverProbability">The initial crossover probability for every individual.</param>
public sealed class JdeVariant(
    double initialMutationForce = JdeStrategy.DefaultInitialMutationForce,
    double initialCrossoverProbability = JdeStrategy.DefaultInitialCrossoverProbability) : IDeVariant
{
    private readonly double _initialMutationForce = initialMutationForce;
    private readonly double _initialCrossoverProbability = initialCrossoverProbability;

    /// <inheritdoc />
    public DeVariantSetup Configure(
        in DeVariantConfiguration configuration)
    {
        var jdeStrategy = new JdeStrategy(
            populationSize: configuration.PopulationSize,
            initialMutationForce: _initialMutationForce,
            initialCrossoverProbability: _initialCrossoverProbability);

        return new DeVariantSetup
        {
            MutationStrategy = new RandMutationStrategy(),
            ControlParameterProvider = jdeStrategy,
            GenerationStrategy = jdeStrategy,
            SelectionStrategy = new SelectionStrategy(configuration.GenomeSize)
        };
    }
}
