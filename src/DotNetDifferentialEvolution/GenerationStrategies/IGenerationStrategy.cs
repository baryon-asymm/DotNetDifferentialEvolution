using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.GenerationStrategies;

/// <summary>
/// A per-generation hook invoked once, single-threaded, after every generation has been
/// fully evaluated and the populations have been swapped. This is where self-adaptive
/// variants update their control-parameter memory, maintain an external archive, and
/// (for L-SHADE) reduce the population size.
/// </summary>
public interface IGenerationStrategy
{
    /// <summary>
    /// Performs end-of-generation bookkeeping and adaptation.
    /// </summary>
    /// <param name="context">
    /// A narrowed view of the run: the just-produced generation, the parents it discarded, the
    /// archive, the fitness ranking and the active population size.
    /// </param>
    /// <param name="trialRecords">
    /// The per-individual trial outcomes for the generation that just finished. Only the
    /// first <see cref="GenerationContext.ActivePopulationSize"/> entries are meaningful.
    /// </param>
    public void AfterGeneration(
        GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords);

    /// <summary>
    /// Adopts the random source the engine supplies for this hook's own bookkeeping — the
    /// randomness it consumes outside the workers, such as JADE/SHADE's random archive eviction.
    /// </summary>
    /// <param name="randomProvider">The random source to draw from.</param>
    /// <remarks>
    /// Called at most once, from <see cref="DifferentialEvolutionBuilder.Build"/>, and only when
    /// <see cref="DifferentialEvolutionBuilder.WithSeed"/> was used. The hook runs single-threaded
    /// on the orchestrator thread, so the provider it receives is used by nothing else. Defaults
    /// to ignoring the provider, so a hook that draws no randomness is unaffected.
    /// </remarks>
    public void UseRandomProvider(
        BaseRandomProvider randomProvider)
    {
    }
}
