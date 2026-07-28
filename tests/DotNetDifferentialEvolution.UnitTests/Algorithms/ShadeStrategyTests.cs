using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.GenerationStrategies;
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

        shade.AfterGeneration(new GenerationContext(context), records);

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

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.Equal(0.5, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    [Fact]
    public void AfterGeneration_WithTerminalCrEnabled_FixesSlotToZeroWhenAllSuccessfulCrAreZero()
    {
        var shade = new TerminalCrShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Every successful trial used CR = 0, so the L-SHADE terminal rule fixes the slot.
        var records = new[]
        {
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.0, UsedF = 0.5 },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.0, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        // A terminal slot yields CR = 0 without drawing the Gaussian: scripting only the slot int
        // and the single Cauchy draw for F would be exhausted if the Gaussian were sampled.
        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5]);
        shade.GetControlParameters(0, draws, out var f, out var cr);

        Assert.Equal(0.0, cr, 1e-12);
        Assert.Equal(0.5, f, 1e-9); // weighted Lehmer of F: (2*0.25 + 4*0.25)/(2*0.5 + 4*0.5) = 0.5
    }

    [Fact]
    public void AfterGeneration_TerminalCrSlotStaysTerminal_EvenAfterNonZeroSuccessfulCr()
    {
        var shade = new TerminalCrShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Generation 1: all-zero successful CR makes slot 0 terminal.
        shade.AfterGeneration(new GenerationContext(context), new[]
        {
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 9, UsedCr = 0.0, UsedF = 0.5 },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 9, UsedCr = 0.0, UsedF = 0.5 },
        });

        // Generation 2: a non-zero successful CR must NOT revive the terminal slot.
        shade.AfterGeneration(new GenerationContext(context), new[]
        {
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 5, UsedCr = 0.9, UsedF = 0.5 },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 5, UsedCr = 0.9, UsedF = 0.5 },
        });

        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5]);
        shade.GetControlParameters(0, draws, out _, out var cr);

        Assert.Equal(0.0, cr, 1e-12);
    }

    [Fact]
    public void AfterGeneration_WithTerminalCrDisabled_KeepsZeroMeanAsAnOrdinaryValue()
    {
        // Plain SHADE (no terminal rule): an all-zero successful CR yields an ordinary M_CR = 0.
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.0, UsedF = 0.5 },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.0, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        // Without the terminal rule the slot holds an ordinary M_CR = 0, so CR is still sampled
        // from a Gaussian (2 draws) rather than being forced to 0 — 3 draws total with the Cauchy
        // draw for F, versus the single draw a terminal slot would consume.
        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5, 0.5, 0.5]);
        shade.GetControlParameters(0, draws, out _, out _);

        Assert.Equal(3, draws.DoubleDrawCount);
    }

    [Fact]
    public void AfterGeneration_IgnoresASuccessWhoseImprovementIsNotMeasurable()
    {
        // Replacing a parent the objective scored NaN is a genuine success — the selection
        // strategy accepts any real-valued trial over it — but the improvement is NaN. Letting it
        // into the weighted means would poison M_F and M_CR for the rest of the run, because
        // `weightSum <= 0.0` is false for NaN and every subsequent division yields NaN.
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Succeeded = true, ParentFfValue = double.NaN, TrialFfValue = 3, UsedCr = 0.1, UsedF = 0.9 },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.False(double.IsNaN(cr));
        Assert.False(double.IsNaN(f));
        // Only the measurable success counts, so the means are exactly its own parameters.
        Assert.Equal(0.9, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    [Fact]
    public void AfterGeneration_IgnoresASuccessOverAnInfiniteParent()
    {
        // Same hazard from the other direction: an infinite improvement would swamp the weighted
        // mean rather than poison it, which is just as wrong.
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord
            {
                Succeeded = true, ParentFfValue = double.PositiveInfinity, TrialFfValue = 3,
                UsedCr = 0.1, UsedF = 0.9
            },
            new TrialRecord { Succeeded = true, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.Equal(0.9, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    private static ProblemContext CreateContext()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitGenerationNumberTerminationStrategy(1);
        return ProblemContextHelper.CreateContext(PopulationSize, evaluator, termination);
    }

    /// <summary>
    /// SHADE with the L-SHADE terminal <c>M_CR</c> rule turned on, to exercise that branch in
    /// isolation (the real consumer is <see cref="DotNetDifferentialEvolution.Algorithms.Lshade.LShadeStrategy"/>).
    /// </summary>
    private sealed class TerminalCrShadeStrategy : ShadeStrategy
    {
        public TerminalCrShadeStrategy(int populationSize, int memorySize, double initialMemoryValue)
            : base(populationSize, memorySize, initialMemoryValue)
        {
        }

        protected override bool UseTerminalCr => true;
    }
}
