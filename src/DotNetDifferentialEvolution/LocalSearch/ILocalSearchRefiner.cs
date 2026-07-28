using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.LocalSearch;

/// <summary>
/// A single-threaded, write-capable refinement hook invoked once at the end of selected
/// generations (every <c>N</c>, see <see cref="DifferentialEvolutionBuilder.WithLocalSearch"/>),
/// after the populations have been swapped and the best individual has been identified.
/// </summary>
/// <remarks>
/// This is the integration point for memetic / hybrid optimization: an implementation may run a
/// local search (e.g. Nelder–Mead) seeded at the current best individual and write the improved
/// solution back into the population. Because it runs on the orchestrator thread between
/// generations, no synchronization with the workers is required.
/// </remarks>
public interface ILocalSearchRefiner
{
    /// <summary>
    /// Refines the current population in place. Implementations typically improve the best
    /// individual — reading its genes through <see cref="ProblemContext.CurrentPopulation"/>, for
    /// instance with <see cref="PopulationView.GenesOf"/>, and writing the improved genes and
    /// fitness back into the same view. Improving the best in place keeps it the best, so the best
    /// index does not need to change.
    /// </summary>
    /// <param name="context">
    /// The problem context for the just-produced generation. Its population buffers, fitness
    /// values, bounds, and <see cref="ProblemContext.BestIndividualIndex"/> are readable and
    /// writable. Implementations MUST add any fitness-function evaluations they perform to
    /// <see cref="ProblemContext.EvaluationCount"/> so evaluation-budget termination stays accurate.
    /// </param>
    /// <param name="generationNumber">The number of the generation that just completed.</param>
    public void Refine(
        ProblemContext context,
        int generationNumber);
}
