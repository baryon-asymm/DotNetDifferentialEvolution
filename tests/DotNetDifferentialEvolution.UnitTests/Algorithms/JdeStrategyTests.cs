using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.Fakes;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Common.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// Tests jDE self-adaptation (Brest et al., 2006): per-individual F/CR are regenerated with
/// small probabilities, and the parameters of successful trials are retained for the owner.
/// </summary>
[Trait("Category", "Unit")]
public class JdeStrategyTests
{
    private const int PopulationSize = 4;

    /// <summary>
    /// With both adaptation probabilities at zero, the strategy hands back the initial F and CR
    /// stored for the individual instead of regenerating them.
    /// </summary>
    [Fact]
    public void WithoutAdaptationReturnsTheStoredPerIndividualParameters()
    {
        var jde = new JdeStrategy(
            PopulationSize,
            initialMutationForce: 0.5,
            initialCrossoverProbability: 0.9,
            fAdaptationProbability: 0.0,
            crAdaptationProbability: 0.0);

        // Adaptation probabilities are 0, so the two "decision" draws never trigger regeneration.
        var random = new ScriptedRandomProvider(doubles: [0.5, 0.5]);

        jde.GetControlParameters(0, random, out var f, out var cr);

        Assert.Equal(0.5, f);
        Assert.Equal(0.9, cr);
    }

    /// <summary>
    /// When adaptation fires, F is redrawn as <c>minF + u * range</c> and CR as a plain uniform
    /// draw, each from its own value draw following the decision draw.
    /// </summary>
    [Fact]
    public void WhenAdaptationTriggersRegeneratesFWithinRangeAndCrUniformly()
    {
        var jde = new JdeStrategy(
            PopulationSize,
            fAdaptationProbability: 1.0,   // decision draw always < 1.0 → always regenerate
            crAdaptationProbability: 1.0,
            minMutationForce: 0.1,
            mutationForceRange: 0.9);

        // F decision 0.0 → regenerate; F value draw 0.5 → 0.1 + 0.5*0.9 = 0.55.
        // CR decision 0.0 → regenerate; CR value draw 0.3 → 0.3.
        var random = new ScriptedRandomProvider(doubles: [0.0, 0.5, 0.0, 0.3]);

        jde.GetControlParameters(0, random, out var f, out var cr);

        Assert.Equal(0.55, f, 1e-12);
        Assert.Equal(0.3, cr, 1e-12);
    }

    /// <summary>
    /// An individual whose trial improved adopts the F and CR that trial used, while an individual
    /// whose parent was kept retains its previous parameters.
    /// </summary>
    [Fact]
    public void AfterGenerationKeepsParametersOfSuccessfulTrialsPerIndividual()
    {
        var jde = new JdeStrategy(
            PopulationSize,
            initialMutationForce: 0.5,
            initialCrossoverProbability: 0.9,
            fAdaptationProbability: 0.0,
            crAdaptationProbability: 0.0);

        var context = CreateContext();
        var records = new TrialRecord[PopulationSize];
        records[0] = new TrialRecord { Outcome = SelectionOutcome.TrialImproved, UsedF = 0.77, UsedCr = 0.33 };
        records[1] = new TrialRecord { Outcome = SelectionOutcome.ParentKept, UsedF = 0.11, UsedCr = 0.22 };

        jde.AfterGeneration(new GenerationContext(context), records);

        // Individual 0 succeeded → its parameters were retained.
        jde.GetControlParameters(0, new ScriptedRandomProvider(doubles: [0.5, 0.5]), out var f0, out var cr0);
        Assert.Equal(0.77, f0);
        Assert.Equal(0.33, cr0);

        // Individual 1 failed → it keeps the initial parameters.
        jde.GetControlParameters(1, new ScriptedRandomProvider(doubles: [0.5, 0.5]), out var f1, out var cr1);
        Assert.Equal(0.5, f1);
        Assert.Equal(0.9, cr1);
    }

    /// <summary>
    /// A trial that survives on a tie replaces its parent, so the individual adopts the trial's
    /// parameters exactly as it would after a strict improvement.
    /// </summary>
    [Fact]
    public void AfterGenerationKeepsParametersOfATrialAcceptedOnATie()
    {
        // jDE attaches the parameters to the individual, so what matters is survival, not
        // improvement: a trial taken on a tie *is* the individual in the next generation, and the
        // parameters it carries are the trial's. Keying this on improvement instead would leave
        // the individual holding parameters that belong to a vector no longer in the population.
        var jde = new JdeStrategy(
            PopulationSize,
            initialMutationForce: 0.5,
            initialCrossoverProbability: 0.9,
            fAdaptationProbability: 0.0,
            crAdaptationProbability: 0.0);

        var context = CreateContext();
        var records = new TrialRecord[PopulationSize];
        records[0] = new TrialRecord { Outcome = SelectionOutcome.TrialAccepted, UsedF = 0.77, UsedCr = 0.33 };
        records[1] = new TrialRecord { Outcome = SelectionOutcome.ParentKept, UsedF = 0.11, UsedCr = 0.22 };

        jde.AfterGeneration(new GenerationContext(context), records);

        jde.GetControlParameters(0, new ScriptedRandomProvider(doubles: [0.5, 0.5]), out var f0, out var cr0);
        Assert.Equal(0.77, f0);
        Assert.Equal(0.33, cr0);

        jde.GetControlParameters(1, new ScriptedRandomProvider(doubles: [0.5, 0.5]), out var f1, out var cr1);
        Assert.Equal(0.5, f1);
        Assert.Equal(0.9, cr1);
    }

    private static ProblemContext CreateContext()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitGenerationNumberTerminationStrategy(1);
        return ProblemContextHelper.CreateContext(PopulationSize, evaluator, termination);
    }
}
