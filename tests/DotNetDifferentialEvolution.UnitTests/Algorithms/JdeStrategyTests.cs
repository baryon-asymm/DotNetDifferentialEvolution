using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// Tests jDE self-adaptation (Brest et al., 2006): per-individual F/CR are regenerated with
/// small probabilities, and the parameters of successful trials are retained for the owner.
/// </summary>
[Trait("Category", "Unit")]
public class JdeStrategyTests
{
    private const int PopulationSize = 4;

    [Fact]
    public void WithoutAdaptation_ReturnsTheStoredPerIndividualParameters()
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

    [Fact]
    public void WhenAdaptationTriggers_RegeneratesFWithinRangeAndCrUniformly()
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

    [Fact]
    public void AfterGeneration_KeepsParametersOfSuccessfulTrialsPerIndividual()
    {
        var jde = new JdeStrategy(
            PopulationSize,
            initialMutationForce: 0.5,
            initialCrossoverProbability: 0.9,
            fAdaptationProbability: 0.0,
            crAdaptationProbability: 0.0);

        var context = CreateContext();
        var records = new TrialRecord[PopulationSize];
        records[0] = new TrialRecord { Succeeded = true, UsedF = 0.77, UsedCr = 0.33 };
        records[1] = new TrialRecord { Succeeded = false, UsedF = 0.11, UsedCr = 0.22 };

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

    private static ProblemContext CreateContext()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitGenerationNumberTerminationStrategy(1);
        return ProblemContextHelper.CreateContext(PopulationSize, evaluator, termination);
    }
}
