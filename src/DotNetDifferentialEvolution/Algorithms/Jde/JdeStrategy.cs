using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Algorithms.Jde;

/// <summary>
/// Implements the self-adaptation scheme of jDE (Brest et al., 2006). Each individual
/// carries its own mutation factor F and crossover probability CR. Before producing a
/// trial these are regenerated with small probabilities; if the trial survives selection,
/// the trial's parameters replace the individual's, so good parameter values propagate.
/// </summary>
/// <remarks>
/// This object plays two roles: an <see cref="IControlParameterProvider"/> that samples
/// per-individual parameters, and an <see cref="IGenerationStrategy"/> that, once per
/// generation, keeps the parameters of successful trials.
/// </remarks>
public class JdeStrategy : IControlParameterProvider, IGenerationStrategy
{
    /// <summary>The default probability of regenerating F (<c>tau1</c>).</summary>
    public const double DefaultFAdaptationProbability = 0.1;

    /// <summary>The default probability of regenerating CR (<c>tau2</c>).</summary>
    public const double DefaultCrAdaptationProbability = 0.1;

    /// <summary>The default lower bound of the regenerated mutation factor.</summary>
    public const double DefaultMinMutationForce = 0.1;

    /// <summary>The default range of the regenerated mutation factor (F in [min, min + range]).</summary>
    public const double DefaultMutationForceRange = 0.9;

    /// <summary>The default initial mutation factor for every individual.</summary>
    public const double DefaultInitialMutationForce = 0.5;

    /// <summary>The default initial crossover probability for every individual.</summary>
    public const double DefaultInitialCrossoverProbability = 0.9;

    private readonly double _fAdaptationProbability;
    private readonly double _crAdaptationProbability;
    private readonly double _minMutationForce;
    private readonly double _mutationForceRange;

    private readonly double[] _mutationForces;
    private readonly double[] _crossoverProbabilities;

    /// <summary>
    /// Initializes a new instance of the <see cref="JdeStrategy"/> class.
    /// </summary>
    /// <param name="populationSize">The size of the population.</param>
    /// <param name="initialMutationForce">The initial mutation factor for every individual.</param>
    /// <param name="initialCrossoverProbability">The initial crossover probability for every individual.</param>
    /// <param name="fAdaptationProbability">The probability of regenerating F (tau1).</param>
    /// <param name="crAdaptationProbability">The probability of regenerating CR (tau2).</param>
    /// <param name="minMutationForce">The lower bound of the regenerated mutation factor.</param>
    /// <param name="mutationForceRange">The range of the regenerated mutation factor.</param>
    public JdeStrategy(
        int populationSize,
        double initialMutationForce = DefaultInitialMutationForce,
        double initialCrossoverProbability = DefaultInitialCrossoverProbability,
        double fAdaptationProbability = DefaultFAdaptationProbability,
        double crAdaptationProbability = DefaultCrAdaptationProbability,
        double minMutationForce = DefaultMinMutationForce,
        double mutationForceRange = DefaultMutationForceRange)
    {
        _fAdaptationProbability = fAdaptationProbability;
        _crAdaptationProbability = crAdaptationProbability;
        _minMutationForce = minMutationForce;
        _mutationForceRange = mutationForceRange;

        _mutationForces = new double[populationSize];
        _crossoverProbabilities = new double[populationSize];
        Array.Fill(_mutationForces, initialMutationForce);
        Array.Fill(_crossoverProbabilities, initialCrossoverProbability);
    }

    /// <inheritdoc />
    public void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        mutationForce = randomProvider.NextDouble() < _fAdaptationProbability
            ? _minMutationForce + randomProvider.NextDouble() * _mutationForceRange
            : _mutationForces[individualIndex];

        crossoverProbability = randomProvider.NextDouble() < _crAdaptationProbability
            ? randomProvider.NextDouble()
            : _crossoverProbabilities[individualIndex];
    }

    /// <inheritdoc />
    public void AfterGeneration(
        GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentPopulationSize = context.ActivePopulationSize;
        for (int i = 0; i < currentPopulationSize; i++)
        {
            // Survival, not improvement. jDE attaches the parameters to the individual, and the
            // individual carried into the next generation is the trial whenever the trial was
            // taken — including on a tie, where the parent it replaced is simply gone.
            if (trialRecords[i].Replaced == false)
                continue;

            _mutationForces[i] = trialRecords[i].UsedF;
            _crossoverProbabilities[i] = trialRecords[i].UsedCr;
        }
    }
}
