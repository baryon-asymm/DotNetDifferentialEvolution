using DotNetDifferentialEvolution.Algorithms.Common;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.Algorithms.Jade;

/// <summary>
/// Implements the parameter adaptation, external archive and p-best ranking of JADE
/// (Zhang &amp; Sanderson, 2009). Mutation factors are sampled from a Cauchy distribution
/// around an adaptive mean <c>μF</c> and crossover rates from a normal distribution around
/// an adaptive mean <c>μCR</c>; both means are nudged toward the values that produced
/// successful trials. Used together with <see cref="MutationStrategies.CurrentToPBestMutationStrategy"/>.
/// </summary>
public class JadeStrategy : AdaptiveStrategyBase, IControlParameterProvider, IGenerationStrategy
{
    /// <summary>The default adaptation rate (<c>c</c>) for the parameter means.</summary>
    public const double DefaultAdaptationRate = 0.1;

    /// <summary>The default initial value of both parameter means.</summary>
    public const double DefaultInitialMean = 0.5;

    private const double CrStandardDeviation = 0.1;
    private const double FScale = 0.1;

    private readonly double _adaptationRate;

    private double _meanCr;
    private double _meanF;

    /// <summary>
    /// Initializes a new instance of the <see cref="JadeStrategy"/> class.
    /// </summary>
    /// <param name="populationSize">The size of the population.</param>
    /// <param name="adaptationRate">The adaptation rate (c) for the parameter means.</param>
    /// <param name="initialMean">The initial value of μF and μCR.</param>
    public JadeStrategy(
        int populationSize,
        double adaptationRate = DefaultAdaptationRate,
        double initialMean = DefaultInitialMean)
        : base(populationSize)
    {
        _adaptationRate = adaptationRate;
        _meanCr = initialMean;
        _meanF = initialMean;
    }

    /// <inheritdoc />
    public void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability)
    {
        crossoverProbability = Math.Clamp(
            RandomDistributionHelper.NextGaussian(randomProvider, _meanCr, CrStandardDeviation), 0.0, 1.0);

        do
        {
            mutationForce = RandomDistributionHelper.NextCauchy(randomProvider, _meanF, FScale);
        } while (mutationForce <= 0.0);

        if (mutationForce > 1.0)
            mutationForce = 1.0;
    }

    /// <inheritdoc />
    public void AfterGeneration(
        GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentPopulationSize = context.ActivePopulationSize;

        UpdateArchive(context, trialRecords, currentPopulationSize);
        AdaptParameterMeans(trialRecords, currentPopulationSize);
    }

    /// <summary>
    /// Nudges μCR toward the arithmetic mean and μF toward the Lehmer mean of the control
    /// parameters that produced successful trials this generation.
    /// </summary>
    private void AdaptParameterMeans(
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        var crSum = 0.0;
        var crCount = 0;
        var fSum = 0.0;
        var fSquaredSum = 0.0;

        for (int i = 0; i < currentPopulationSize; i++)
        {
            // S_CR and S_F take improving trials only; a trial accepted on a tie taught the search
            // nothing and must not pull μCR or μF toward its parameters.
            if (trialRecords[i].Improved == false)
                continue;

            crSum += trialRecords[i].UsedCr;
            fSum += trialRecords[i].UsedF;
            fSquaredSum += trialRecords[i].UsedF * trialRecords[i].UsedF;
            crCount++;
        }

        if (crCount == 0)
            return;

        _meanCr = (1.0 - _adaptationRate) * _meanCr + _adaptationRate * (crSum / crCount);
        if (fSum > 0.0)
            _meanF = (1.0 - _adaptationRate) * _meanF + _adaptationRate * (fSquaredSum / fSum);
    }
}
