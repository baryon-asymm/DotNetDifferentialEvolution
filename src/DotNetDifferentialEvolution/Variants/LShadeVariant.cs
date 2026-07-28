using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// L-SHADE (Tanabe &amp; Fukunaga, 2014): SHADE plus Linear Population Size Reduction, the
/// CEC-2014 competition winner. The population shrinks linearly from its initial size toward 4 as
/// the evaluation budget is consumed.
/// </summary>
public sealed class LShadeVariant : IDeVariant
{
    private readonly long _maxEvaluationNumber;
    private readonly double _pBestRate;
    private readonly double _archiveSizeRate;
    private readonly int _memorySize;

    /// <summary>
    /// Initializes a new instance of the <see cref="LShadeVariant"/> class.
    /// </summary>
    /// <param name="maxEvaluationNumber">The fitness-evaluation budget driving the reduction.</param>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the current population size.</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    public LShadeVariant(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6)
    {
        if (maxEvaluationNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEvaluationNumber), "Evaluation budget must be greater than 0.");
        if (archiveSizeRate < 0.0)
            throw new ArgumentOutOfRangeException(nameof(archiveSizeRate), "Archive size rate must be non-negative.");

        _maxEvaluationNumber = maxEvaluationNumber;
        _pBestRate = pBestRate;
        _archiveSizeRate = archiveSizeRate;
        _memorySize = memorySize;
    }

    /// <inheritdoc />
    public DeVariantSetup Configure(
        in DeVariantConfiguration configuration)
    {
        var lShadeStrategy = new LShadeStrategy(
            initialPopulationSize: configuration.PopulationSize,
            maxEvaluationNumber: _maxEvaluationNumber,
            archiveSizeRate: _archiveSizeRate,
            memorySize: _memorySize);

        return new DeVariantSetup
        {
            MutationStrategy = new CurrentToPBestMutationStrategy(_pBestRate),
            ControlParameterProvider = lShadeStrategy,
            GenerationStrategy = lShadeStrategy,
            SelectionStrategy = new SelectionStrategy(configuration.GenomeSize),
            ArchiveCapacity = ArchiveCapacityHelper.Size(_archiveSizeRate, configuration.PopulationSize)
        };
    }

    /// <inheritdoc />
    public void Validate(
        in DeVariantConfiguration configuration,
        ITerminationStrategy terminationStrategy)
    {
        // The population schedule is a function of the budget: reduction reaches the minimum
        // exactly when the budget is exhausted. A run stopped by a different evaluation limit
        // either terminates with a population still well above 4, or spends its tail at 4.
        if (terminationStrategy is LimitEvaluationNumberTerminationStrategy evaluationTermination
            && evaluationTermination.MaxEvaluationNumber != _maxEvaluationNumber)
            throw new InvalidOperationException(
                $"L-SHADE was configured with an evaluation budget of {_maxEvaluationNumber}, but the " +
                $"termination strategy limits evaluations to {evaluationTermination.MaxEvaluationNumber}. " +
                "They must match so the linear population-size reduction reaches its minimum exactly " +
                "as the run terminates.");
    }
}
