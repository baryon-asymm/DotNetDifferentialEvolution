using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// Tests SHADE success-history memory updates (Tanabe &amp; Fukunaga, 2013): a memory slot is
/// overwritten with the improvement-weighted arithmetic mean of CR and weighted Lehmer mean
/// of F. A single-slot memory (H = 1) makes the sampled slot deterministic.
/// </summary>
[Trait("Category", "Unit")]
public class ShadeStrategyTests
{
    private const int PopulationSize = 2;

    // slot = Next(1) = 0; then Gaussian (→ μCR) and Cauchy (→ μF) reveal the memory cell.
    private static ScriptedRandomProvider CellRevealingDraws() =>
        new(ints: [0], doubles: [0.5, 0.75, 0.5]);

    [Fact]
    public void Constructor_ThrowsWhenMemorySizeIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShadeStrategy(PopulationSize, memorySize: 0));
    }

    [Fact]
    public void AfterGeneration_StoresImprovementWeightedMeans()
    {
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Weights are the fitness improvements: rec0 w=2, rec1 w=4.
        var records = new[]
        {
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.4, UsedF = 0.2 },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5 },
        };

        shade.AfterGeneration(context, records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        // CR: (2*0.4 + 4*0.9) / 6 = 0.733333…
        Assert.Equal((2 * 0.4 + 4 * 0.9) / 6.0, cr, 1e-9);
        // F (weighted Lehmer): (2*0.2² + 4*0.5²) / (2*0.2 + 4*0.5) = 1.08 / 2.4 = 0.45
        Assert.Equal((2 * 0.04 + 4 * 0.25) / (2 * 0.2 + 4 * 0.5), f, 1e-9);
    }

    [Fact]
    public void AfterGeneration_WithNoSuccesses_LeavesMemoryUnchanged()
    {
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Succeeded = false, UsedCr = 0.1, UsedF = 0.1 },
            new TrialRecord { Succeeded = false, UsedCr = 0.9, UsedF = 0.9 },
        };

        shade.AfterGeneration(context, records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

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
