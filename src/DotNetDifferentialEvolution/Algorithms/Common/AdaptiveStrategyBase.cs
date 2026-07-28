using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Algorithms.Common;

/// <summary>
/// Shared machinery for the JADE/SHADE/L-SHADE family: external-archive maintenance and
/// fitness ranking for the <c>current-to-pbest/1</c> mutation strategy.
/// </summary>
public abstract class AdaptiveStrategyBase
{
    /// <summary>A single-threaded random provider for end-of-generation bookkeeping.</summary>
    protected BaseRandomProvider RandomProvider { get; private set; } = new RandomProvider();

    /// <summary>
    /// Adopts the seeded random source the engine supplies, so that archive eviction — the one
    /// place this family draws randomness outside the workers — is reproducible too.
    /// </summary>
    /// <param name="randomProvider">The random source to draw from.</param>
    public void UseRandomProvider(
        BaseRandomProvider randomProvider)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        RandomProvider = randomProvider;
    }

    private readonly double[] _sortKeys;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveStrategyBase"/> class.
    /// </summary>
    /// <param name="populationSize">The maximum population size (used to size scratch buffers).</param>
    protected AdaptiveStrategyBase(
        int populationSize)
    {
        _sortKeys = new double[populationSize];
    }

    /// <summary>
    /// Adds the parents discarded this generation (now residing in the swapped-out trial
    /// population buffer) to the external archive, evicting at random when the archive is full.
    /// </summary>
    protected void UpdateArchive(
        GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        ArgumentNullException.ThrowIfNull(context);

        var archive = context.Archive.Span;
        var genomeSize = context.GenomeSize;
        var bufferCapacity = genomeSize == 0 ? 0 : archive.Length / genomeSize;
        // Not `== 0`: a hook is free to set ArchiveCapacity, and a negative one would slip past an
        // equality test into Next(capacity), which throws from inside a running generation rather
        // than being ignored the way a disabled archive is.
        var capacity = Math.Min(context.ArchiveCapacity, bufferCapacity);
        if (capacity <= 0)
            return;

        var discardedParents = context.DiscardedParents.Genes.Span;
        var archiveSize = Math.Min(context.ArchiveSize, capacity);

        for (int i = 0; i < currentPopulationSize; i++)
        {
            // Improvement, not survival: both papers insert into the archive on the strict
            // comparison (their Algorithm 2, line 16), so a parent displaced by a tie is not
            // archived. It was not beaten, and the archive exists to keep beaten parents around.
            if (trialRecords[i].Improved == false)
                continue;

            int slot;
            if (archiveSize < capacity)
                slot = archiveSize++;
            else
                slot = RandomProvider.Next(capacity);

            discardedParents.Slice(i * genomeSize, genomeSize)
                .CopyTo(archive.Slice(slot * genomeSize, genomeSize));
        }

        context.ArchiveSize = archiveSize;
    }

    /// <summary>
    /// Re-ranks the active population by fitness into <see cref="ProblemContext.FitnessSortedIndices"/>.
    /// </summary>
    /// <remarks>
    /// The engine owns the ranking: it rebuilds it at the end of every generation whenever the
    /// configured mutation strategy declares
    /// <see cref="MutationStrategies.Interfaces.MutationRequirements.FitnessRanking"/>. This
    /// remains available for a strategy that needs a ranking of its own — L-SHADE calls it when it
    /// is paired with a mutation strategy that asked for none — but adaptation code should not
    /// maintain the shared ranking as a side effect of its own bookkeeping.
    /// </remarks>
    protected void RebuildSortedIndices(
        GenerationContext context,
        int currentPopulationSize)
    {
        ArgumentNullException.ThrowIfNull(context);

        PopulationSortHelper.SortIndicesByFitness(
            context.FitnessSortedIndices.Span,
            context.CurrentPopulation.FfValues.Span,
            currentPopulationSize,
            _sortKeys);
    }
}
