using DotNetDifferentialEvolution.Algorithms.Lshade;
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

        lshade.AfterGeneration(context, new TrialRecord[InitialPopulationSize]);

        Assert.Equal(expectedPopulationSize, context.CurrentPopulationSize);
    }

    [Fact]
    public void AfterGeneration_KeepsTheBestSurvivorsInAscendingFitnessOrder()
    {
        var lshade = CreateStrategy();
        var context = CreateContext();
        context.EvaluationCount = 50; // reduces 10 → 7

        var originalFitness = context.PopulationFfValues.ToArray();

        lshade.AfterGeneration(context, new TrialRecord[InitialPopulationSize]);

        var newSize = context.CurrentPopulationSize;
        var expectedSurvivors = originalFitness.OrderBy(v => v).Take(newSize).ToArray();
        var actualSurvivors = context.PopulationFfValues.Span[..newSize].ToArray();

        Assert.Equal(expectedSurvivors, actualSurvivors); // best `newSize`, ascending
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

    private static ProblemContext CreateContext()
    {
        var evaluator = new SphereEvaluator(dimension: 2);
        var termination = new LimitEvaluationNumberTerminationStrategy(MaxEvaluations);
        return ProblemContextHelper.CreateContext(InitialPopulationSize, evaluator, termination);
    }
}
