using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Algorithms.Lshade;

/// <summary>
/// Implements L-SHADE (Tanabe &amp; Fukunaga, 2014): SHADE plus Linear Population Size
/// Reduction (LPSR). The population shrinks linearly from its initial size down to
/// <see cref="MinimumPopulationSize"/> as the evaluation budget is consumed; at each
/// reduction the worst individuals are dropped and the archive capacity is scaled down to
/// match. L-SHADE was the winner of the CEC-2014 competition.
/// </summary>
public class LShadeStrategy : ShadeStrategy
{
    /// <summary>The smallest population size L-SHADE will reduce to.</summary>
    public const int MinimumPopulationSize = 4;

    private readonly int _initialPopulationSize;
    private readonly int _minPopulationSize;
    private readonly long _maxEvaluationNumber;
    private readonly double _archiveSizeRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="LShadeStrategy"/> class.
    /// </summary>
    /// <param name="initialPopulationSize">The initial (maximum) population size.</param>
    /// <param name="maxEvaluationNumber">The fitness-evaluation budget driving the reduction.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the current population size.</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    /// <param name="minPopulationSize">The smallest population size to reduce to.</param>
    public LShadeStrategy(
        int initialPopulationSize,
        long maxEvaluationNumber,
        double archiveSizeRate,
        int memorySize,
        int minPopulationSize = MinimumPopulationSize)
        : base(initialPopulationSize, memorySize)
    {
        if (minPopulationSize < 4)
            throw new ArgumentOutOfRangeException(nameof(minPopulationSize), "Minimum population size must be at least 4.");
        if (initialPopulationSize < minPopulationSize)
            throw new ArgumentException("Initial population size must be at least the minimum population size.");

        _initialPopulationSize = initialPopulationSize;
        _minPopulationSize = minPopulationSize;
        _maxEvaluationNumber = maxEvaluationNumber;
        _archiveSizeRate = archiveSizeRate;
    }

    /// <inheritdoc />
    public override void AfterGeneration(
        ProblemContext context,
        ReadOnlySpan<TrialRecord> trialRecords)
    {
        ArgumentNullException.ThrowIfNull(context);

        // SHADE bookkeeping: archive, success-history memory, and the fitness ranking that
        // LPSR relies on to identify the survivors.
        base.AfterGeneration(context, trialRecords);

        ReducePopulationSize(context);
    }

    /// <summary>
    /// Linearly reduces the active population size based on the evaluation budget consumed,
    /// keeping the best individuals and scaling the archive capacity to match.
    /// </summary>
    private void ReducePopulationSize(
        ProblemContext context)
    {
        var currentPopulationSize = context.CurrentPopulationSize;
        var newPopulationSize = Math.Clamp(
            ComputePlannedPopulationSize(context.EvaluationCount), _minPopulationSize, currentPopulationSize);

        if (newPopulationSize >= currentPopulationSize)
            return;

        var genomeSize = context.GenomeSize;
        var population = context.Population.Span;
        var populationFfValues = context.PopulationFfValues.Span;
        var sortedIndices = context.FitnessSortedIndices.Span;

        // Use the swapped-out trial buffers as scratch to gather the best survivors (the
        // ranking is ascending, so the first newPopulationSize indices are the survivors).
        var scratch = context.TrialPopulation.Span;
        var scratchFfValues = context.TrialPopulationFfValues.Span;

        for (int k = 0; k < newPopulationSize; k++)
        {
            var sourceIndex = sortedIndices[k];
            population.Slice(sourceIndex * genomeSize, genomeSize)
                .CopyTo(scratch.Slice(k * genomeSize, genomeSize));
            scratchFfValues[k] = populationFfValues[sourceIndex];
        }

        scratch.Slice(0, newPopulationSize * genomeSize).CopyTo(population);
        scratchFfValues.Slice(0, newPopulationSize).CopyTo(populationFfValues);

        context.CurrentPopulationSize = newPopulationSize;

        // The survivors are now stored in ascending-fitness order, so the ranking is identity.
        for (int k = 0; k < newPopulationSize; k++)
            sortedIndices[k] = k;

        // Scale the archive capacity down and drop any overflow entries.
        var newArchiveCapacity = (int)Math.Round(_archiveSizeRate * newPopulationSize);
        context.ArchiveCapacity = newArchiveCapacity;
        if (context.ArchiveSize > newArchiveCapacity)
            context.ArchiveSize = newArchiveCapacity;
    }

    /// <summary>
    /// Computes the planned population size for the consumed evaluation budget using the
    /// linear schedule <c>N = round((N_min - N_init) / maxEvals * evals + N_init)</c>.
    /// </summary>
    private int ComputePlannedPopulationSize(
        long evaluationCount)
    {
        var progress = Math.Min(1.0, (double)evaluationCount / _maxEvaluationNumber);
        return (int)Math.Round(
            (_minPopulationSize - _initialPopulationSize) * progress + _initialPopulationSize);
    }
}
