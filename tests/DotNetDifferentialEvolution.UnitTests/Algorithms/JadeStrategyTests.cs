using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// Tests JADE parameter-mean adaptation (Zhang &amp; Sanderson, 2009): μCR is nudged toward
/// the arithmetic mean and μF toward the Lehmer mean of the successful trials' parameters.
/// </summary>
/// <remarks>
/// μF and μCR are private; they are observed indirectly by sampling. With the scripted draws
/// below, the Gaussian sampler collapses to exactly μCR (cos term forced to 0) and the Cauchy
/// sampler collapses to exactly μF (tan term forced to 0), revealing the adapted means.
/// </remarks>
[Trait("Category", "Unit")]
public class JadeStrategyTests
{
    private const int PopulationSize = 2;

    // u_cr1 = anything (multiplied by 0), u_cr2 = 0.75 → Gaussian = μCR; u_f = 0.5 → Cauchy = μF.
    private static double[] MeanRevealingDraws => [0.5, 0.75, 0.5];

    [Fact]
    public void AfterGeneration_NudgesMeansTowardSuccessfulParameters()
    {
        var jade = new JadeStrategy(PopulationSize, adaptationRate: 0.1, initialMean: 0.5);
        var context = CreateContext();

        // CR = {0.4, 0.8} → arithmetic mean 0.6; F = {0.2, 0.8} → Lehmer mean 0.68.
        var records = new[]
        {
            new TrialRecord { Succeeded = true, UsedCr = 0.4, UsedF = 0.2, ParentFfValue = 2, TrialFfValue = 1 },
            new TrialRecord { Succeeded = true, UsedCr = 0.8, UsedF = 0.8, ParentFfValue = 2, TrialFfValue = 1 },
        };

        jade.AfterGeneration(new GenerationContext(context), records);

        jade.GetControlParameters(0, new ScriptedRandomProvider(doubles: MeanRevealingDraws), out var f, out var cr);

        Assert.Equal(0.9 * 0.5 + 0.1 * 0.6, cr, 1e-9);   // μCR = 0.51
        Assert.Equal(0.9 * 0.5 + 0.1 * 0.68, f, 1e-9);   // μF  = 0.518
    }

    [Fact]
    public void AfterGeneration_WithNoSuccesses_LeavesMeansUnchanged()
    {
        var jade = new JadeStrategy(PopulationSize, adaptationRate: 0.1, initialMean: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Succeeded = false, UsedCr = 0.9, UsedF = 0.9 },
            new TrialRecord { Succeeded = false, UsedCr = 0.1, UsedF = 0.1 },
        };

        jade.AfterGeneration(new GenerationContext(context), records);

        jade.GetControlParameters(0, new ScriptedRandomProvider(doubles: MeanRevealingDraws), out var f, out var cr);

        Assert.Equal(0.5, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    private static ProblemContext CreateContext()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitGenerationNumberTerminationStrategy(1);
        return ProblemContextHelper.CreateContext(PopulationSize, evaluator, termination);
    }
}
