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

    /// <summary>
    /// Sentinel stored in an <c>M_CR</c> slot once it becomes terminal (<c>⊥</c>): any negative
    /// value is outside the valid CR range [0, 1] and forces sampled CR to 0 (L-SHADE, 2014).
    /// </summary>
    private const double TerminalCrValue = -1.0;

    private readonly int _memorySize;
    private readonly double[] _memoryCr;
    private readonly double[] _memoryF;

    private int _memoryIndex;

    /// <summary>
    /// Gets a value indicating whether the L-SHADE terminal <c>M_CR</c> rule is applied: when a
    /// memory slot's successful CR values are all 0 it is fixed at a terminal value, after which
    /// it always samples CR = 0. SHADE (2013) does not use it (<see langword="false"/>); L-SHADE
    /// (2014) overrides this to <see langword="true"/>.
    /// </summary>
    protected virtual bool UseTerminalCr => false;

    /// <summary>
    /// Gets a value indicating whether <c>M_CR</c> is updated with the weighted <em>Lehmer</em>
    /// mean rather than the weighted arithmetic mean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two papers genuinely differ here, and the difference is not cosmetic. SHADE (2013),
    /// Eq. (17), specifies <c>mean_WA(S_CR)</c> — this class's default. L-SHADE (2014) is built on
    /// SHADE 1.1, whose memory update (its Algorithm 1, line 5) specifies <c>mean_WL(S_CR)</c>,
    /// the same weighted Lehmer mean already used for <c>M_F</c>.
    /// </para>
    /// <para>
    /// The direction of the discrepancy is fixed, not incidental:
    /// <c>mean_WL - mean_WA = Var_w(S_CR) / E_w(S_CR) &gt;= 0</c>, so the arithmetic mean always
    /// reports the lower value. That downward pull on <c>M_CR</c> is precisely what the Lehmer
    /// mean was introduced to remove, and under <see cref="UseTerminalCr"/> it compounds: the
    /// lower <c>M_CR</c> drifts, the likelier a generation is to produce <c>max(S_CR) = 0</c> and
    /// lock the slot at CR = 0 for the rest of the run.
    /// </para>
    /// <para>
    /// Kept separate from <see cref="UseTerminalCr"/> even though both arrived in SHADE 1.1, so
    /// that each rule can be turned on — and tested — on its own.
    /// </para>
    /// </remarks>
    protected virtual bool UseLehmerCrMean => false;

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
        {
            throw new ArgumentOutOfRangeException(nameof(memorySize), "Memory size must be greater than 0.");
        }

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

        // A terminal M_CR slot deterministically yields CR = 0 (no Gaussian draw).
        crossoverProbability = _memoryCr[slot] < 0.0
            ? 0.0
            : Math.Clamp(
                RandomDistributionHelper.NextGaussian(randomProvider, _memoryCr[slot], CrStandardDeviation), 0.0, 1.0);

        do
        {
            mutationForce = RandomDistributionHelper.NextCauchy(randomProvider, _memoryF[slot], FScale);
        } while (mutationForce <= 0.0);

        if (mutationForce > 1.0)
        {
            mutationForce = 1.0;
        }
    }

    /// <inheritdoc />
    public virtual void AfterGeneration(
        GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentPopulationSize = context.ActivePopulationSize;

        UpdateArchive(context, trialRecords, currentPopulationSize);
        UpdateMemory(trialRecords, currentPopulationSize);
    }

    /// <summary>
    /// Overwrites one memory slot with the success-weighted means of this generation's
    /// successful parameters: the weighted Lehmer mean for F, and for CR either the weighted
    /// arithmetic mean (SHADE) or the weighted Lehmer mean (L-SHADE, see
    /// <see cref="UseLehmerCrMean"/>).
    /// </summary>
    /// <remarks>
    /// Each mean is a ratio of two sums of weights, so it does not depend on the scale of the
    /// weights. That is what lets the sums be kept finite: when the largest weight of the
    /// generation is so large that <c>N</c> of them (times F or CR, at most 1) could overflow, every
    /// weight is divided by it first. Below that bound the divisor is 1 and the weights enter the
    /// sums exactly as they are.
    /// </remarks>
    private void UpdateMemory(
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        var weightScale = WeightScale(trialRecords, currentPopulationSize);

        var weightSum = 0.0;
        var weightedCrSum = 0.0;
        var weightedCrSquaredSum = 0.0;
        var weightedFSum = 0.0;
        var weightedFSquaredSum = 0.0;
        var maxSuccessfulCr = 0.0;

        for (var i = 0; i < currentPopulationSize; i++)
        {
            if (!TryGetWeight(trialRecords[i], out var unscaledWeight))
            {
                continue;
            }

            var weight = unscaledWeight / weightScale;
            var cr = trialRecords[i].UsedCr;
            var f = trialRecords[i].UsedF;

            weightSum += weight;
            weightedCrSum += weight * cr;
            weightedCrSquaredSum += weight * cr * cr;
            weightedFSum += weight * f;
            weightedFSquaredSum += weight * f * f;
            if (cr > maxSuccessfulCr)
            {
                maxSuccessfulCr = cr;
            }
        }

        if (weightSum <= 0.0)
        {
            return;
        }

        // L-SHADE terminal rule: once a slot's successful CR values are all 0 (or it is already
        // terminal), it stays terminal and forever samples CR = 0.
        var isTerminal = UseTerminalCr && (_memoryCr[_memoryIndex] < 0.0 || maxSuccessfulCr <= 0.0);

        // The Lehmer branch divides by the weighted sum of CR, which is zero only when every
        // successful CR is zero. Under L-SHADE that case is the terminal rule above, so this is
        // unreachable there; the guard is what keeps the mean well defined for a subclass that
        // takes SHADE 1.1's mean without its terminal rule.
        var crMean = UseLehmerCrMean && weightedCrSum > 0.0
            ? weightedCrSquaredSum / weightedCrSum
            : weightedCrSum / weightSum;

        _memoryCr[_memoryIndex] = isTerminal ? TerminalCrValue : crMean;

        if (weightedFSum > 0.0)
        {
            _memoryF[_memoryIndex] = weightedFSquaredSum / weightedFSum;
        }

        _memoryIndex = (_memoryIndex + 1) % _memorySize;
    }

    /// <summary>
    /// Finds the divisor for this generation's weights: the largest weight when it exceeds
    /// <c>double.MaxValue / (2 · N)</c>, else 1. A finite weight can still overflow the sums
    /// (parents the objective scored <see cref="double.MaxValue"/> leave improvements near
    /// <see cref="double.MaxValue"/>, and their sum is <c>+∞</c>, whose quotient <c>∞/∞</c> would
    /// write NaN into <c>M_F</c> and <c>M_CR</c>). Divided by the largest weight, no weight exceeds
    /// 1 and, with F and CR at most 1, no sum exceeds <c>N</c>.
    /// </summary>
    private static double WeightScale(
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        var largestWeight = 0.0;
        for (var i = 0; i < currentPopulationSize; i++)
        {
            if (TryGetWeight(trialRecords[i], out var weight) && weight > largestWeight)
            {
                largestWeight = weight;
            }
        }

        return largestWeight > double.MaxValue / (2.0 * currentPopulationSize)
            ? largestWeight
            : 1.0;
    }

    /// <summary>
    /// Takes the weight of a record: its fitness improvement, when it has a usable one.
    /// </summary>
    private static bool TryGetWeight(
        in TrialRecord record,
        out double weight)
    {
        weight = 0.0;

        // S_CR and S_F take improving trials only (both papers, Algorithm 2 line 16). A trial
        // accepted on a tie has an improvement of exactly zero, so it would enter the weighted
        // means with weight zero and contribute nothing but the risk of an empty weight sum.
        if (!record.Improved)
        {
            return false;
        }

        // Weight by the fitness improvement. A success does not always come with a finite,
        // strictly positive one: replacing a parent the objective scored NaN — or an infinite
        // one — is a genuine success with an unmeasurable improvement. Such a record cannot be
        // weighted, and letting it through would put NaN into weightSum, which the
        // weightSum <= 0.0 guard in UpdateMemory does not catch (every comparison against NaN is
        // false), permanently poisoning M_F and M_CR for the rest of the run.
        var improvement = record.ParentFfValue - record.TrialFfValue;
        if (!double.IsFinite(improvement))
        {
            return false;
        }

        weight = improvement;
        return true;
    }
}
