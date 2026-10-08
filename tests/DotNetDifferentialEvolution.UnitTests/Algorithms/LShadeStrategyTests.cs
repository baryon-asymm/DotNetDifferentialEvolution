using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.Fakes;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Common.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// Tests L-SHADE (Tanabe &amp; Fukunaga, 2014): Linear Population Size Reduction — the active
/// population shrinks linearly with the consumed evaluation budget, keeping the best individuals
/// and never dropping below the minimum — and the parts of SHADE 1.1's memory update that
/// L-SHADE inherits but plain SHADE (2013) does not.
/// </summary>
[Trait("Category", "Unit")]
public class LShadeStrategyTests
{
    private const int InitialPopulationSize = 10;
    private const long MaxEvaluations = 100;

    /// <summary>
    /// The active population follows the linear reduction schedule of the consumed evaluation
    /// budget, from the initial size down to the minimum, and never below it once the budget is
    /// overrun.
    /// </summary>
    /// <param name="evaluationCount">The evaluations consumed so far.</param>
    /// <param name="expectedPopulationSize">The population size the schedule prescribes.</param>
    [Theory]
    // N = round((minN - initN)/maxEvals * evals + initN), minN = 4, initN = 10, maxEvals = 100.
    [InlineData(0L, 10)]     // no budget consumed → no reduction
    [InlineData(50L, 7)]     // halfway → 7
    [InlineData(100L, 4)]    // budget exhausted → minimum
    [InlineData(200L, 4)]    // over budget → clamped to the minimum
    public void AfterGenerationReducesPopulationLinearlyWithTheEvaluationBudget(
        long evaluationCount,
        int expectedPopulationSize)
    {
        var lshade = CreateStrategy();
        var context = CreateContext();
        context.EvaluationCount = evaluationCount;

        lshade.AfterGeneration(new GenerationContext(context), new TrialRecord[InitialPopulationSize]);

        Assert.Equal(expectedPopulationSize, context.CurrentPopulationSize);
    }

    /// <summary>
    /// Shrinking the population keeps exactly the best individuals and leaves them ordered from
    /// best to worst.
    /// </summary>
    [Fact]
    public void AfterGenerationKeepsTheBestSurvivorsInAscendingFitnessOrder()
    {
        var lshade = CreateStrategy();
        var context = CreateContext();
        context.EvaluationCount = 50; // reduces 10 → 7

        var originalFitness = context.CurrentPopulation.FfValues.ToArray();

        lshade.AfterGeneration(new GenerationContext(context), new TrialRecord[InitialPopulationSize]);

        var newSize = context.CurrentPopulationSize;
        var expectedSurvivors = originalFitness.OrderBy(v => v).Take(newSize).ToArray();
        var actualSurvivors = context.CurrentPopulation.FfValues.Span[..newSize].ToArray();

        Assert.Equal(expectedSurvivors, actualSurvivors); // best `newSize`, ascending
    }

    /// <summary>
    /// A scheduled population size that falls exactly halfway between two integers is rounded
    /// half away from zero, as the papers do, not to the even neighbour.
    /// </summary>
    /// <param name="initialPopulationSize">The population size the run starts with.</param>
    /// <param name="maxEvaluationNumber">The total evaluation budget.</param>
    /// <param name="evaluationCount">The evaluations consumed so far.</param>
    /// <param name="expectedPopulationSize">The population size rounded half up.</param>
    [Theory]
    // Inputs for which N = round((minN - initN)/maxEvals * evals + initN) lands on an exact
    // midpoint (minN = 4). The papers round half away from zero; .NET's default
    // MidpointRounding.ToEven rounds each of these down to the even neighbour instead.
    [InlineData(24, 2000L, 750L, 17)]   // 24 - 20 * 0.375  = 16.5 → 17 (ToEven gives 16)
    [InlineData(8, 1000L, 375L, 7)]     //  8 -  4 * 0.375  =  6.5 →  7 (ToEven gives  6)
    [InlineData(12, 1600L, 300L, 11)]   // 12 -  8 * 0.1875 = 10.5 → 11 (ToEven gives 10)
    public void AfterGenerationRoundsMidpointPopulationSizesHalfUp(
        int initialPopulationSize,
        long maxEvaluationNumber,
        long evaluationCount,
        int expectedPopulationSize)
    {
        var lshade = new LShadeStrategy(
            initialPopulationSize: initialPopulationSize,
            maxEvaluationNumber: maxEvaluationNumber,
            archiveSizeRate: 0.0,
            memorySize: 5);
        var context = CreateContext(initialPopulationSize, maxEvaluationNumber);
        context.EvaluationCount = evaluationCount;

        lshade.AfterGeneration(new GenerationContext(context), new TrialRecord[initialPopulationSize]);

        Assert.Equal(expectedPopulationSize, context.CurrentPopulationSize);
    }

    /// <summary>
    /// The archive capacity rescaled to the reduced population is rounded half away from zero
    /// when it lands on a midpoint.
    /// </summary>
    [Fact]
    public void AfterGenerationRoundsAMidpointArchiveCapacityHalfUp()
    {
        // Half the budget reduces 10 → 7 individuals; 1.5 * 7 = 10.5 is an exact midpoint,
        // which MidpointRounding.ToEven would round down to 10.
        var lshade = new LShadeStrategy(
            initialPopulationSize: InitialPopulationSize,
            maxEvaluationNumber: MaxEvaluations,
            archiveSizeRate: 1.5,
            memorySize: 5);
        var context = CreateContext();
        context.EvaluationCount = 50;

        lshade.AfterGeneration(new GenerationContext(context), new TrialRecord[InitialPopulationSize]);

        Assert.Equal(7, context.CurrentPopulationSize);
        Assert.Equal(11, context.ArchiveCapacity);
    }

    /// <summary>
    /// L-SHADE writes the improvement-weighted Lehmer mean of the successful CR values into the
    /// memory, which sits above the arithmetic mean plain SHADE uses on the same inputs.
    /// </summary>
    [Fact]
    public void AfterGenerationUpdatesMemoryCrWithTheWeightedLehmerMean()
    {
        // L-SHADE is built on SHADE 1.1, whose memory update takes the weighted *Lehmer* mean of
        // the successful CR values (its Algorithm 1, line 5). SHADE (2013), Eq. (17), takes the
        // weighted arithmetic mean — and ShadeStrategyTests pins that on these very inputs, so
        // the two tests read as a pair. The gap is Var_w(S_CR) / E_w(S_CR), always in this
        // direction, which is why taking the arithmetic mean here biased M_CR downward.
        var lshade = CreateStrategy(memorySize: 1);
        var context = CreateContext();

        // Weights are the fitness improvements: rec0 w = 2, rec1 w = 4.
        var records = new TrialRecord[InitialPopulationSize];
        records[0] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = 10,
            TrialFfValue = 8,
            UsedCr = 0.4,
            UsedF = 0.2
        };
        records[1] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = 10,
            TrialFfValue = 6,
            UsedCr = 0.9,
            UsedF = 0.5
        };

        lshade.AfterGeneration(new GenerationContext(context), records);

        lshade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        var weightedLehmer = (2 * 0.4 * 0.4 + 4 * 0.9 * 0.9) / (2 * 0.4 + 4 * 0.9); // 0.80909…
        var weightedArithmetic = (2 * 0.4 + 4 * 0.9) / 6.0;                         // 0.73333…
        Assert.Equal(weightedLehmer, cr, 1e-9);
        Assert.True(cr > weightedArithmetic, "the Lehmer mean must sit above the arithmetic one");

        // F was already the weighted Lehmer mean in both papers and must not have moved.
        Assert.Equal((2 * 0.04 + 4 * 0.25) / (2 * 0.2 + 4 * 0.5), f, 1e-9);
    }

    /// <summary>
    /// When every successful CR is zero, the terminal rule fixes the memory slot so that it yields
    /// CR = 0 without a Gaussian draw, instead of computing a 0/0 Lehmer mean.
    /// </summary>
    [Fact]
    public void AfterGenerationTerminalCrRuleWinsOverTheLehmerMean()
    {
        // Both halves of SHADE 1.1's rule are on for L-SHADE, and the terminal test comes first:
        // all-zero successful CR fixes the slot rather than feeding a 0/0 Lehmer mean.
        var lshade = CreateStrategy(memorySize: 1);
        var context = CreateContext();

        var records = new TrialRecord[InitialPopulationSize];
        records[0] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = 10,
            TrialFfValue = 8,
            UsedCr = 0.0,
            UsedF = 0.5
        };
        records[1] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = 10,
            TrialFfValue = 6,
            UsedCr = 0.0,
            UsedF = 0.5
        };

        lshade.AfterGeneration(new GenerationContext(context), records);

        // A terminal slot yields CR = 0 without drawing the Gaussian, so a script holding only
        // the slot index and the single Cauchy draw for F suffices.
        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5]);
        lshade.GetControlParameters(0, draws, out _, out var cr);

        Assert.Equal(0.0, cr, 1e-12);
    }

    /// <summary>
    /// Two successes over parents scored <see cref="double.MaxValue"/> overflow the sums, and the
    /// Lehmer memory must still come out finite and hold the successes' own parameters (O1).
    /// </summary>
    [Fact]
    public void AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums()
    {
        var lshade = CreateStrategy(memorySize: 1);
        var context = CreateContext();

        var records = new TrialRecord[InitialPopulationSize];
        records[0] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = double.MaxValue,
            TrialFfValue = 1,
            UsedCr = 0.9,
            UsedF = 1.0
        };
        records[1] = records[0];

        lshade.AfterGeneration(new GenerationContext(context), records);

        lshade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.True(double.IsFinite(cr), $"CR is {cr}");
        Assert.True(double.IsFinite(f), $"F is {f}");
        Assert.Equal(1.0, f, 1e-12);
        Assert.Equal(0.9, cr, 1e-12);
    }

    /// <summary>
    /// A success whose improvement is the largest double, beside one of improvement 1, decides the
    /// Lehmer memory as the sums would without the scale.
    /// </summary>
    [Fact]
    public void AfterGenerationWeighsAnImprovementNearTheLargestDoubleAgainstOneOfOne()
    {
        var lshade = CreateStrategy(memorySize: 1);
        var context = CreateContext();

        var records = new TrialRecord[InitialPopulationSize];
        records[0] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = double.MaxValue,
            TrialFfValue = 1,
            UsedCr = 0.3,
            UsedF = 0.8
        };
        records[1] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = 2,
            TrialFfValue = 1,
            UsedCr = 0.9,
            UsedF = 0.2
        };

        lshade.AfterGeneration(new GenerationContext(context), records);

        lshade.GetControlParameters(0, CellRevealingDraws(), out var f, out var cr);

        Assert.True(double.IsFinite(cr), $"CR is {cr}");
        Assert.True(double.IsFinite(f), $"F is {f}");
        Assert.Equal(0.8, f, 1e-12);
        Assert.Equal(0.3, cr, 1e-12);
    }

    /// <summary>
    /// Below the overflow bound the scale is not applied: over 200 random sets of two generations
    /// the memory, terminal slots included, equals 6.0.0's unscaled arithmetic bit for bit (O2).
    /// </summary>
    [Fact]
    public void AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit() =>
        UnscaledShadeMemory.AssertTheStrategyEqualsTheUnscaledArithmetic(
            seed: 20261009,
            useTerminalCr: true,
            useLehmerCrMean: true,
            allZeroCrInTheFirstSet: true,
            createStrategy: (populationSize, memorySize) => new LShadeStrategy(
                initialPopulationSize: populationSize,
                maxEvaluationNumber: MaxEvaluations,
                archiveSizeRate: 1.0,
                memorySize: memorySize),
            createContext: populationSize => CreateContext(populationSize));

    /// <summary>
    /// A zero or negative evaluation budget is rejected at construction, since it would divide the
    /// reduction schedule by a non-positive number.
    /// </summary>
    /// <param name="maxEvaluationNumber">The invalid evaluation budget.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ConstructorRejectsANonPositiveEvaluationBudget(
        long maxEvaluationNumber)
    {
        // The budget is the denominator of the reduction schedule. Left unchecked it produces a
        // non-finite progress and collapses the population to the minimum in one generation,
        // which no exception ever reports.
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LShadeStrategy(
            initialPopulationSize: InitialPopulationSize,
            maxEvaluationNumber: maxEvaluationNumber,
            archiveSizeRate: 0.0,
            memorySize: 5));
    }

    /// <summary>
    /// A negative archive size rate is rejected at construction.
    /// </summary>
    [Fact]
    public void ConstructorRejectsANegativeArchiveSizeRate()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new LShadeStrategy(
            initialPopulationSize: InitialPopulationSize,
            maxEvaluationNumber: MaxEvaluations,
            archiveSizeRate: -0.5,
            memorySize: 5));
    }

    /// <summary>
    /// The constructor rejects a minimum population size below the floor of four, and one that
    /// exceeds the initial population size.
    /// </summary>
    /// <param name="minPopulationSize">The invalid minimum population size.</param>
    [Theory]
    [InlineData(3)]                       // below the floor of 4
    [InlineData(InitialPopulationSize)]   // equal handled separately; this checks > initial
    public void ConstructorValidatesMinimumPopulationSize(
        int minPopulationSize)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => new LShadeStrategy(
            initialPopulationSize: minPopulationSize < 4 ? InitialPopulationSize : 4,
            maxEvaluationNumber: MaxEvaluations,
            archiveSizeRate: 0.0,
            memorySize: 5,
            minPopulationSize: minPopulationSize));
    }

    private static LShadeStrategy CreateStrategy(
        int memorySize = 5) => new(
        initialPopulationSize: InitialPopulationSize,
        maxEvaluationNumber: MaxEvaluations,
        archiveSizeRate: 0.0,
        memorySize: memorySize);

    /// <summary>
    /// Draws that hand back the memory cell unchanged: slot <c>Next(1) = 0</c>, then the Gaussian
    /// pair whose standard normal is 0 and the Cauchy draw whose deviate is 0.
    /// </summary>
    private static ScriptedRandomProvider CellRevealingDraws() =>
        new(ints: [0], doubles: [0.5, 0.75, 0.5]);

    private static ProblemContext CreateContext(
        int populationSize = InitialPopulationSize,
        long maxEvaluations = MaxEvaluations)
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitEvaluationNumberTerminationStrategy(maxEvaluations);
        return ProblemContextHelper.CreateContext(populationSize, evaluator, termination);
    }
}
