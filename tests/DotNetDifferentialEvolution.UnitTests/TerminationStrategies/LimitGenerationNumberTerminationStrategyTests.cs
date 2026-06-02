using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.UnitTests.TestSupport;

namespace DotNetDifferentialEvolution.UnitTests.TerminationStrategies;

/// <summary>
/// Tests the generation-count termination strategy.
/// </summary>
[Trait("Category", "Unit")]
public class LimitGenerationNumberTerminationStrategyTests
{
    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(99, 100, false)]
    [InlineData(100, 100, true)]
    [InlineData(101, 100, true)]
    public void TerminatesOnceTheGenerationLimitIsReached(
        int generationNumber,
        int maxGenerationNumber,
        bool expected)
    {
        var strategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber);
        var population = PopulationFactory.Create(
            genes: [0.0], fitnessValues: [0.0], generationNumber: generationNumber);

        Assert.Equal(expected, strategy.ShouldTerminate(population));
    }
}
