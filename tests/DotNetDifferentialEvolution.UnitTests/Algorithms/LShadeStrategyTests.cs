using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// Tests L-SHADE Linear Population Size Reduction (Tanabe &amp; Fukunaga, 2014): the active
/// population shrinks linearly with the consumed evaluation budget, keeping the best
/// individuals and never dropping below the minimum.
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

    private static LShadeStrategy CreateStrategy() => new(
        initialPopulationSize: InitialPopulationSize,
        maxEvaluationNumber: MaxEvaluations,
        archiveSizeRate: 0.0,
        memorySize: 5);

    private static ProblemContext CreateContext(
        int populationSize = InitialPopulationSize,
        long maxEvaluations = MaxEvaluations)
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitEvaluationNumberTerminationStrategy(maxEvaluations);
        return ProblemContextHelper.CreateContext(populationSize, evaluator, termination);
    }
}
