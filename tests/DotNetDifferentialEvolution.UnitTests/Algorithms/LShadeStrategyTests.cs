using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

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

    [Theory]
    // N = round((minN - initN)/maxEvals * evals + initN), minN = 4, initN = 10, maxEvals = 100.
    [InlineData(0L, 10)]     // no budget consumed → no reduction
    [InlineData(50L, 7)]     // halfway → 7
    [InlineData(100L, 4)]    // budget exhausted → minimum
    [InlineData(200L, 4)]    // over budget → clamped to the minimum
    public void AfterGeneration_ReducesPopulationLinearlyWithTheEvaluationBudget(
        long evaluationCount,
        int expectedPopulationSize)
    {
        var lshade = CreateStrategy();
        var context = CreateContext();
        context.EvaluationCount = evaluationCount;

        lshade.AfterGeneration(new GenerationContext(context), new TrialRecord[InitialPopulationSize]);

        Assert.Equal(expectedPopulationSize, context.CurrentPopulationSize);
    }

    [Fact]
    public void AfterGeneration_KeepsTheBestSurvivorsInAscendingFitnessOrder()
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

    [Theory]
    // Inputs for which N = round((minN - initN)/maxEvals * evals + initN) lands on an exact
    // midpoint (minN = 4). The papers round half away from zero; .NET's default
    // MidpointRounding.ToEven rounds each of these down to the even neighbour instead.
    [InlineData(24, 2000L, 750L, 17)]   // 24 - 20 * 0.375  = 16.5 → 17 (ToEven gives 16)
    [InlineData(8, 1000L, 375L, 7)]     //  8 -  4 * 0.375  =  6.5 →  7 (ToEven gives  6)
    [InlineData(12, 1600L, 300L, 11)]   // 12 -  8 * 0.1875 = 10.5 → 11 (ToEven gives 10)
    public void AfterGeneration_RoundsMidpointPopulationSizesHalfUp(
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

    [Fact]
    public void AfterGeneration_RoundsAMidpointArchiveCapacityHalfUp()
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

    [Fact]
    public void AfterGeneration_UpdatesMemoryCrWithTheWeightedLehmerMean()
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
            Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.4, UsedF = 0.2
        };
        records[1] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.9, UsedF = 0.5
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

    [Fact]
    public void AfterGeneration_TerminalCrRuleWinsOverTheLehmerMean()
    {
        // Both halves of SHADE 1.1's rule are on for L-SHADE, and the terminal test comes first:
        // all-zero successful CR fixes the slot rather than feeding a 0/0 Lehmer mean.
        var lshade = CreateStrategy(memorySize: 1);
        var context = CreateContext();

        var records = new TrialRecord[InitialPopulationSize];
        records[0] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 8, UsedCr = 0.0, UsedF = 0.5
        };
        records[1] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved, ParentFfValue = 10, TrialFfValue = 6, UsedCr = 0.0, UsedF = 0.5
        };

        lshade.AfterGeneration(new GenerationContext(context), records);

        // A terminal slot yields CR = 0 without drawing the Gaussian, so a script holding only
        // the slot index and the single Cauchy draw for F suffices.
        var draws = new ScriptedRandomProvider(ints: [0], doubles: [0.5]);
        lshade.GetControlParameters(0, draws, out _, out var cr);

        Assert.Equal(0.0, cr, 1e-12);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Constructor_RejectsANonPositiveEvaluationBudget(
        long maxEvaluationNumber)
    {
        // The budget is the denominator of the reduction schedule. Left unchecked it produces a
        // non-finite progress and collapses the population to the minimum in one generation,
        // which no exception ever reports.
        Assert.Throws<ArgumentOutOfRangeException>(() => new LShadeStrategy(
            initialPopulationSize: InitialPopulationSize,
            maxEvaluationNumber: maxEvaluationNumber,
            archiveSizeRate: 0.0,
            memorySize: 5));
    }

    [Fact]
    public void Constructor_RejectsANegativeArchiveSizeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LShadeStrategy(
            initialPopulationSize: InitialPopulationSize,
            maxEvaluationNumber: MaxEvaluations,
            archiveSizeRate: -0.5,
            memorySize: 5));
    }

    [Theory]
    [InlineData(3)]                       // below the floor of 4
    [InlineData(InitialPopulationSize)]   // equal handled separately; this checks > initial
    public void Constructor_ValidatesMinimumPopulationSize(
        int minPopulationSize)
    {
        Assert.ThrowsAny<ArgumentException>(() => new LShadeStrategy(
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
