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
    protected BaseRandomProvider RandomProvider { get; } = new RandomProvider();

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
        ProblemContext context,
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        ArgumentNullException.ThrowIfNull(context);

        var archive = context.Archive.Span;
        var genomeSize = context.GenomeSize;
        var bufferCapacity = genomeSize == 0 ? 0 : archive.Length / genomeSize;
        var capacity = Math.Min(context.ArchiveCapacity, bufferCapacity);
        if (capacity == 0)
            return;

        var discardedParents = context.TrialPopulation.Span;
        var archiveSize = Math.Min(context.ArchiveSize, capacity);

        for (int i = 0; i < currentPopulationSize; i++)
        {
            if (trialRecords[i].Succeeded == false)
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
    protected void RebuildSortedIndices(
        ProblemContext context,
        int currentPopulationSize)
    {
        ArgumentNullException.ThrowIfNull(context);

        PopulationSortHelper.SortIndicesByFitness(
            context.FitnessSortedIndices.Span,
            context.PopulationFfValues.Span,
            currentPopulationSize,
            _sortKeys);
    }
}
