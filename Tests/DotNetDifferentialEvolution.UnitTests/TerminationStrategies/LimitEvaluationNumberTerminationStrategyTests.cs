using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.UnitTests.TestSupport;

namespace DotNetDifferentialEvolution.UnitTests.TerminationStrategies;

/// <summary>
/// Tests the evaluation-budget termination strategy (the natural stop for L-SHADE).
/// </summary>
[Trait("Category", "Unit")]
public class LimitEvaluationNumberTerminationStrategyTests
{
    [Theory]
    [InlineData(0L, 10_000L, false)]
    [InlineData(9_999L, 10_000L, false)]
    [InlineData(10_000L, 10_000L, true)]
    [InlineData(10_001L, 10_000L, true)]
    public void TerminatesOnceTheEvaluationBudgetIsReached(
        long evaluationCount,
        long maxEvaluationNumber,
        bool expected)
    {
        var strategy = new LimitEvaluationNumberTerminationStrategy(maxEvaluationNumber);
        var population = PopulationFactory.Create(
            genes: [0.0], fitnessValues: [0.0], evaluationCount: evaluationCount);

        Assert.Equal(expected, strategy.ShouldTerminate(population));
    }
}
