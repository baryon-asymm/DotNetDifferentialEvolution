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
    /// <param name="context">The problem context (current population is the just-produced generation).</param>
    /// <param name="trialRecords">
    /// The per-individual trial outcomes for the generation that just finished. Only the
    /// first <see cref="ProblemContext.CurrentPopulationSize"/> entries are meaningful.
    /// </param>
    public void AfterGeneration(
        ProblemContext context,
        ReadOnlySpan<TrialRecord> trialRecords);
}
