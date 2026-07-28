using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.GenerationStrategies;

/// <summary>
/// What an <see cref="IGenerationStrategy"/> is allowed to see and change between generations:
/// the population, the parents just discarded, the external archive, the fitness ranking, the
/// evaluation count, and the size of the active population.
/// </summary>
/// <remarks>
/// This is deliberately narrower than the engine's own <see cref="ProblemContext"/>. A hook is a
/// third-party extension point invoked in the middle of a run; handing it the whole context also
/// handed it the population swap, the best-individual index, the evaluation counter, the
/// termination strategy and the buffers themselves, none of which it has any business rewriting.
/// Everything here is something at least one published variant genuinely needs.
/// </remarks>
public sealed class GenerationContext
{
    private readonly ProblemContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationContext"/> class.
    /// </summary>
    /// <param name="context">The problem context to expose a narrowed view of.</param>
    /// <remarks>
    /// The engine builds one per run. It is public so that a generation strategy can be exercised
    /// against a context of your own construction, which is what the built-ins' own tests do.
    /// </remarks>
    public GenerationContext(
        ProblemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>
    /// Gets the generation that just finished, in the buffer it was produced into. Its genes and
    /// fitness values are writable — L-SHADE compacts the survivors through them.
    /// </summary>
    public PopulationView CurrentPopulation => _context.CurrentPopulation;

    /// <summary>
    /// Gets the buffer holding the parents this generation discarded — what the external archive
    /// stores. It is also free scratch until the next generation starts writing into it.
    /// </summary>
    public PopulationView DiscardedParents => _context.TrialPopulation;

    /// <summary>
    /// Gets or sets the number of live individuals. Narrowing it drops the individuals above the
    /// new size from the run; both population buffers are narrowed together.
    /// </summary>
    public int ActivePopulationSize
    {
        get => _context.CurrentPopulationSize;
        set => _context.CurrentPopulationSize = value;
    }

    /// <summary>
    /// Gets the total number of fitness-function evaluations performed so far, including the
    /// generation that just finished. A budget-driven schedule reads it; it is not a hook's to set.
    /// </summary>
    public long EvaluationCount => _context.EvaluationCount;

    /// <summary>
    /// Gets the flattened genes of the external archive of discarded parents.
    /// </summary>
    public Memory<double> Archive => _context.Archive;

    /// <summary>
    /// Gets or sets the number of individuals currently stored in <see cref="Archive"/>.
    /// </summary>
    public int ArchiveSize
    {
        get => _context.ArchiveSize;
        set => _context.ArchiveSize = value;
    }

    /// <summary>
    /// Gets or sets the maximum number of individuals the archive may hold. L-SHADE scales it down
    /// with the population; it never exceeds the allocated buffer.
    /// </summary>
    public int ArchiveCapacity
    {
        get => _context.ArchiveCapacity;
        set => _context.ArchiveCapacity = value;
    }

    /// <summary>
    /// Gets the population indices ordered ascending by fitness, best first. The engine rebuilds
    /// this before the hook runs whenever the mutation strategy declared
    /// <see cref="MutationRequirements.FitnessRanking"/>, so a hook that needs it can normally
    /// read it rather than compute it.
    /// </summary>
    public Memory<int> FitnessSortedIndices => _context.FitnessSortedIndices;

    /// <summary>
    /// Gets what the configured mutation strategy declared it needs. A hook consults this to find
    /// out what the engine is already maintaining on its behalf.
    /// </summary>
    public MutationRequirements MutationRequirements => _context.MutationRequirements;

    /// <summary>Gets the number of genes per individual.</summary>
    public int GenomeSize => _context.GenomeSize;
}
