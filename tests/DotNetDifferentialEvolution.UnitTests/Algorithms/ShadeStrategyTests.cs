using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.Fakes;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Common.Helpers;

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

    /// <summary>Finite fitness differences retain their relative weights at either end of the double range.</summary>
    /// <param name="lShade">Whether CR uses the Lehmer mean.</param>
    /// <param name="overflow">Whether the second difference overflows (1:2), or both are subnormal (1:3).</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExtremeImprovementsRetainTheirRelativeWeights(bool lShade, bool overflow)
    {
        const int count = 4;
        var strategy = lShade ? new LShadeStrategy(count, 100, 0, 1) : new ShadeStrategy(count, 1);
        var context = ProblemContextHelper.CreateContext(count, new SphereEvaluator(2), new LimitGenerationNumberTerminationStrategy(1));
        var records = new TrialRecord[]
        {
            new() { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = overflow ? double.MaxValue : double.Epsilon, TrialFfValue = 0, UsedF = 0.25, UsedCr = 0.75 },
            new() { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = overflow ? double.MaxValue : double.Epsilon, TrialFfValue = overflow ? -double.MaxValue : -2 * double.Epsilon, UsedF = 0.75, UsedCr = 0.25 },
            new() { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = double.PositiveInfinity, TrialFfValue = 0, UsedF = 1, UsedCr = 1 },
            new() { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 1, TrialFfValue = double.NegativeInfinity, UsedF = 1, UsedCr = 1 },
        };

        strategy.AfterGeneration(new GenerationContext(context), records);
        UnscaledShadeMemory.ReadBack(strategy, 0, terminal: false, out var f, out var cr);

        Assert.Equal(overflow ? 19.0 / 28 : 0.7, f);
        var expectedCr = overflow ? (lShade ? 11.0 / 20 : 5.0 / 12) : (lShade ? 0.5 : 0.375);
        Assert.Equal(expectedCr, cr);
    }

    /// <summary>
    /// A success-history memory needs at least one slot, so a memory size of zero is rejected.
    /// </summary>
    [Fact]
    public void ConstructorThrowsWhenMemorySizeIsNotPositive() => _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ShadeStrategy(PopulationSize, memorySize: 0));

    /// <summary>The written means retain representable ratios when individual products lose precision.</summary>
    /// <param name="lShade">Whether CR uses the Lehmer mean.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExtremeProductsRetainRepresentableMeans(bool lShade)
    {
        const int count = 4;
        var strategy = lShade ? new LShadeStrategy(count, 100, 0, 1) : new ShadeStrategy(count, 1);
        var context = ProblemContextHelper.CreateContext(count, new SphereEvaluator(2), new LimitGenerationNumberTerminationStrategy(1));
        var tiny = Math.ScaleB(1, -1000);
        var factor = Math.ScaleB(1, -54);
        var small = Math.ScaleB(1, -600);
        (double W1, double W2, double X1, double X2, double Lehmer, double Arithmetic)[] cases =
        [
            (Math.ScaleB(1, -969), Math.ScaleB(1, -968), factor, factor, factor, factor),
            (1, 1, small, small, small, small),
            (1, tiny, tiny, 1, 0.5, 2 * tiny),
            (Math.ScaleB(1, 1000), Math.ScaleB(1, -50), Math.ScaleB(1, -1050), 1, 0.5, Math.ScaleB(1, -1049)),
            (tiny, 2 * tiny, tiny, tiny, tiny, tiny),
            (1, 1, double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon),
        ];
        foreach (var (w1, w2, x1, x2, lehmer, arithmetic) in cases)
        {
            var records = new TrialRecord[count];
            records[0] = new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = w1, UsedF = x1, UsedCr = x1 };
            records[^1] = new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = w2, UsedF = x2, UsedCr = x2 };
            strategy.AfterGeneration(new GenerationContext(context), records);
            UnscaledShadeMemory.ReadBack(strategy, 0, terminal: false, out var f, out var cr);
            Assert.Equal(lehmer, f);
            Assert.Equal(lShade ? lehmer : arithmetic, cr);
        }

        var zeros = new TrialRecord[count];
        zeros[0] = new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 1, UsedF = small, UsedCr = -0.0 };
        zeros[^1] = new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 1, UsedF = small, UsedCr = double.Epsilon };
        strategy.AfterGeneration(new GenerationContext(context), zeros);
        UnscaledShadeMemory.ReadBack(strategy, 0, terminal: false, out var tinyF, out var tinyCr);
        Assert.Equal(small, tinyF);
        Assert.Equal(lShade ? double.Epsilon : 0.0, tinyCr);
    }

    /// <summary>
    /// The memory slot receives the improvement-weighted arithmetic mean of the successful CR values
    /// and the improvement-weighted Lehmer mean of the successful F values.
    /// </summary>
    [Fact]
    public void AfterGenerationStoresImprovementWeightedMeans()
    {
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Weights are the fitness improvements: rec0 w=2, rec1 w=4.
        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.4, UsedF = 0.2 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        // CR: (2*0.4 + 4*0.9) / 6 = 0.733333…
        Assert.Equal((2 * 0.4 + 4 * 0.9) / 6.0, cr, 1e-9);
        // F (weighted Lehmer): (2*0.2² + 4*0.5²) / (2*0.2 + 4*0.5) = 1.08 / 2.4 = 0.45
        Assert.Equal((2 * 0.04 + 4 * 0.25) / (2 * 0.2 + 4 * 0.5), f, 1e-9);
    }

    /// <summary>
    /// A generation without a single improving trial leaves the memory slot at its initial value.
    /// </summary>
    [Fact]
    public void AfterGenerationWithNoSuccessesLeavesMemoryUnchanged()
    {
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.ParentKept, UsedCr = 0.1, UsedF = 0.1 },
            new TrialRecord { Outcome = SelectionOutcome.ParentKept, UsedCr = 0.9, UsedF = 0.9 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.Equal(0.5, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    /// <summary>
    /// With the terminal rule on, a generation whose successful trials all used CR = 0 marks the slot
    /// terminal: it then yields CR = 0 without drawing the Gaussian, while F is still updated.
    /// </summary>
    [Fact]
    public void AfterGenerationWithTerminalCrEnabledFixesSlotToZeroWhenAllSuccessfulCrAreZero()
    {
        var shade = new TerminalCrShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Every successful trial used CR = 0, so the L-SHADE terminal rule fixes the slot.
        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.0, UsedF = 0.5 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.0, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        // A terminal slot yields CR = 0 without drawing the Gaussian: scripting only the slot int
        // and the single Cauchy draw for F would be exhausted if the Gaussian were sampled.
        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5]);
        shade.GetControlParameters(0, draws, out var f, out var cr);

        Assert.Equal(0.0, cr, 1e-12);
        Assert.Equal(0.5, f, 1e-9); // weighted Lehmer of F: (2*0.25 + 4*0.25)/(2*0.5 + 4*0.5) = 0.5
    }

    /// <summary>
    /// Once a slot has become terminal, a later generation of successes with non-zero CR does not
    /// revive it.
    /// </summary>
    [Fact]
    public void AfterGenerationTerminalCrSlotStaysTerminalEvenAfterNonZeroSuccessfulCr()
    {
        var shade = new TerminalCrShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Generation 1: all-zero successful CR makes slot 0 terminal.
        shade.AfterGeneration(new GenerationContext(context),
        [
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 9, UsedCr = 0.0, UsedF = 0.5 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 9, UsedCr = 0.0, UsedF = 0.5 },
        ]);

        // Generation 2: a non-zero successful CR must NOT revive the terminal slot.
        shade.AfterGeneration(new GenerationContext(context),
        [
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 5, UsedCr = 0.9, UsedF = 0.5 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 5, UsedCr = 0.9, UsedF = 0.5 },
        ]);

        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5]);
        shade.GetControlParameters(0, draws, out _, out var cr);

        Assert.Equal(0.0, cr, 1e-12);
    }

    /// <summary>
    /// Without the terminal rule, an all-zero successful CR is stored as an ordinary mean of zero, so CR
    /// is still sampled from the Gaussian.
    /// </summary>
    [Fact]
    public void AfterGenerationWithTerminalCrDisabledKeepsZeroMeanAsAnOrdinaryValue()
    {
        // Plain SHADE (no terminal rule): an all-zero successful CR yields an ordinary M_CR = 0.
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.0, UsedF = 0.5 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.0, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        // Without the terminal rule the slot holds an ordinary M_CR = 0, so CR is still sampled
        // from a Gaussian (2 draws) rather than being forced to 0 — 3 draws total with the Cauchy
        // draw for F, versus the single draw a terminal slot would consume.
        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5, 0.5, 0.5]);
        shade.GetControlParameters(0, draws, out _, out _);

        Assert.Equal(3, draws.DoubleDrawCount);
    }

    /// <summary>
    /// A success over a parent scored NaN has a NaN improvement and is left out of the weighted means,
    /// so it cannot turn the memory into NaN.
    /// </summary>
    [Fact]
    public void AfterGenerationIgnoresASuccessWhoseImprovementIsNotMeasurable()
    {
        // Replacing a parent the objective scored NaN is a genuine success — the selection
        // strategy accepts any real-valued trial over it — but the improvement is NaN. Letting it
        // into the weighted means would poison M_F and M_CR for the rest of the run, because
        // `weightSum <= 0.0` is false for NaN and every subsequent division yields NaN.
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = double.NaN, TrialFfValue = 3, UsedCr = 0.1, UsedF = 0.9 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.False(double.IsNaN(cr));
        Assert.False(double.IsNaN(f));
        // Only the measurable success counts, so the means are exactly its own parameters.
        Assert.Equal(0.9, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    /// <summary>
    /// A success over a parent with infinite fitness has an infinite improvement and is left out of the
    /// weighted means, so it cannot swamp them.
    /// </summary>
    [Fact]
    public void AfterGenerationIgnoresASuccessOverAnInfiniteParent()
    {
        // Same hazard from the other direction: an infinite improvement would swamp the weighted
        // mean rather than poison it, which is just as wrong.
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord
            {
                Outcome = SelectionOutcome.TrialImproved, ParentFfValue = double.PositiveInfinity, TrialFfValue = 3,
                UsedCr = 0.1, UsedF = 0.9
            },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.Equal(0.9, cr, 1e-9);
        Assert.Equal(0.5, f, 1e-9);
    }

    /// <summary>
    /// Two successes over parents scored <see cref="double.MaxValue"/> each have a finite improvement,
    /// and the sums of those improvements overflow; the memory must still come out finite and hold
    /// the successes' own parameters (O1).
    /// </summary>
    [Fact]
    public void AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums()
    {
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        // Each weight is double.MaxValue - 1 = double.MaxValue, finite; their sum is +∞, and the
        // unscaled means were ∞/∞ = NaN.
        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = double.MaxValue, TrialFfValue = 1, UsedCr = 0.9, UsedF = 1.0 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = double.MaxValue, TrialFfValue = 1, UsedCr = 0.9, UsedF = 1.0 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.True(double.IsFinite(cr), $"CR is {cr}");
        Assert.True(double.IsFinite(f), $"F is {f}");
        Assert.Equal(1.0, f, 1e-12);
        Assert.Equal(0.9, cr, 1e-12);
    }

    /// <summary>
    /// A success whose improvement is the largest double, beside one of improvement 1, is weighted as
    /// the sums would weigh it without the scale: the large one's parameters are the memory's.
    /// </summary>
    [Fact]
    public void AfterGenerationWeighsAnImprovementNearTheLargestDoubleAgainstOneOfOne()
    {
        var shade = new ShadeStrategy(PopulationSize, memorySize: 1, initialMemoryValue: 0.5);
        var context = CreateContext();

        var records = new[]
        {
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = double.MaxValue, TrialFfValue = 1, UsedCr = 0.3, UsedF = 0.8 },
            new TrialRecord { Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 2, TrialFfValue = 1, UsedCr = 0.9, UsedF = 0.2 },
        };

        shade.AfterGeneration(new GenerationContext(context), records);

        shade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.True(double.IsFinite(cr), $"CR is {cr}");
        Assert.True(double.IsFinite(f), $"F is {f}");
        Assert.Equal(0.8, f, 1e-12);
        Assert.Equal(0.3, cr, 1e-12);
    }

    /// <summary>
    /// Below the overflow bound the scale is not applied: over 200 random sets of two generations
    /// the memory equals 6.0.0's unscaled arithmetic bit for bit (O2).
    /// </summary>
    [Fact]
    public void AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit() =>
        UnscaledShadeMemory.AssertTheStrategyEqualsTheUnscaledArithmetic(
            seed: 20261008,
            useTerminalCr: false,
            useLehmerCrMean: false,
            allZeroCrInEveryFourthSet: false,
            createStrategy: (populationSize, memorySize) => new ShadeStrategy(populationSize, memorySize, initialMemoryValue: 0.5),
            createContext: populationSize => ProblemContextHelper.CreateContext(
                populationSize,
                new SphereEvaluator(dimension: 2),
                new LimitGenerationNumberTerminationStrategy(1)));

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
    private sealed class TerminalCrShadeStrategy(int populationSize, int memorySize, double initialMemoryValue) : ShadeStrategy(populationSize, memorySize, initialMemoryValue)
    {
        protected override bool UseTerminalCr => true;
    }
}
