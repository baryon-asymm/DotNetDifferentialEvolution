using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// jDE (Brest et al., 2006): <c>DE/rand/1/bin</c> with per-individual self-adapting F and CR and
/// greedy selection. Each individual carries its own control parameters, which are re-sampled
/// with small probability each generation and inherited by a successful trial.
/// </summary>
public sealed class JdeVariant : IDeVariant
{
    private readonly double _initialMutationForce;
    private readonly double _initialCrossoverProbability;

    /// <summary>
    /// Initializes a new instance of the <see cref="JdeVariant"/> class.
    /// </summary>
    /// <param name="initialMutationForce">The initial mutation factor for every individual.</param>
    /// <param name="initialCrossoverProbability">The initial crossover probability for every individual.</param>
    public JdeVariant(
        double initialMutationForce = JdeStrategy.DefaultInitialMutationForce,
        double initialCrossoverProbability = JdeStrategy.DefaultInitialCrossoverProbability)
    {
        _initialMutationForce = initialMutationForce;
        _initialCrossoverProbability = initialCrossoverProbability;
    }

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
