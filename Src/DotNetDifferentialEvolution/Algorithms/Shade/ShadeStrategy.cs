using DotNetDifferentialEvolution.Algorithms.Common;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.Algorithms.Shade;

/// <summary>
/// Implements the success-history based parameter adaptation of SHADE (Tanabe &amp;
/// Fukunaga, 2013). Instead of JADE's single adaptive mean, SHADE keeps a memory of
/// <c>H</c> pairs <c>(M_F, M_CR)</c>. Each individual picks a random memory slot to sample
/// its F and CR; each generation, one slot is overwritten with the success-weighted means
/// of the parameters that produced improving trials. Reuses JADE's external archive and
/// <see cref="MutationStrategies.CurrentToPBestMutationStrategy"/>.
/// </summary>
public class ShadeStrategy : AdaptiveStrategyBase, IControlParameterProvider, IGenerationStrategy
{
    /// <summary>The default size of the success-history memory (H).</summary>
    public const int DefaultMemorySize = 100;

    /// <summary>The default initial value stored in every memory slot.</summary>
    public const double DefaultInitialMemoryValue = 0.5;

    private const double CrStandardDeviation = 0.1;
    private const double FScale = 0.1;

    private readonly int _memorySize;
    private readonly double[] _memoryCr;
    private readonly double[] _memoryF;

    private int _memoryIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShadeStrategy"/> class.
    /// </summary>
    /// <param name="populationSize">The size of the population.</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    /// <param name="initialMemoryValue">The initial value stored in every memory slot.</param>
    public ShadeStrategy(
        int populationSize,
        int memorySize = DefaultMemorySize,
        double initialMemoryValue = DefaultInitialMemoryValue)
        : base(populationSize)
    {
        if (memorySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(memorySize), "Memory size must be greater than 0.");

        _memorySize = memorySize;
        _memoryCr = new double[memorySize];
        _memoryF = new double[memorySize];
        Array.Fill(_memoryCr, initialMemoryValue);
        Array.Fill(_memoryF, initialMemoryValue);
    }

    /// <inheritdoc />
    public void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        var slot = randomProvider.Next(_memorySize);

        crossoverProbability = Math.Clamp(
            RandomDistributionHelper.NextGaussian(randomProvider, _memoryCr[slot], CrStandardDeviation), 0.0, 1.0);

        do
        {
            mutationForce = RandomDistributionHelper.NextCauchy(randomProvider, _memoryF[slot], FScale);
        } while (mutationForce <= 0.0);

        if (mutationForce > 1.0)
            mutationForce = 1.0;
    }

    /// <inheritdoc />
    public virtual void AfterGeneration(
        ProblemContext context,
        ReadOnlySpan<TrialRecord> trialRecords)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentPopulationSize = context.CurrentPopulationSize;

        UpdateArchive(context, trialRecords, currentPopulationSize);
        UpdateMemory(trialRecords, currentPopulationSize);
        RebuildSortedIndices(context, currentPopulationSize);
    }

    /// <summary>
    /// Overwrites one memory slot with the success-weighted means of this generation's
    /// successful parameters (weighted arithmetic mean for CR, weighted Lehmer mean for F).
    /// </summary>
    private void UpdateMemory(
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        var weightSum = 0.0;
        var weightedCrSum = 0.0;
        var weightedFSum = 0.0;
        var weightedFSquaredSum = 0.0;

        for (int i = 0; i < currentPopulationSize; i++)
        {
            if (trialRecords[i].Succeeded == false)
                continue;

            // Weight by the fitness improvement; success implies a strictly positive delta.
            var weight = trialRecords[i].ParentFfValue - trialRecords[i].TrialFfValue;
            var cr = trialRecords[i].UsedCr;
            var f = trialRecords[i].UsedF;

            weightSum += weight;
            weightedCrSum += weight * cr;
            weightedFSum += weight * f;
            weightedFSquaredSum += weight * f * f;
        }

        if (weightSum <= 0.0)
            return;

        _memoryCr[_memoryIndex] = weightedCrSum / weightSum;
        if (weightedFSum > 0.0)
            _memoryF[_memoryIndex] = weightedFSquaredSum / weightedFSum;

        _memoryIndex = (_memoryIndex + 1) % _memorySize;
    }
}
