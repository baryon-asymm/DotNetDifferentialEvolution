using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>Runs built through retained stages of one CPU builder.</summary>
[Trait("Category", "Integration")]
public class BuilderBoundsTests
{
    /// <summary>Default selection copies complete individuals using each build's current genome size.</summary>
    /// <param name="firstSize">The genome size when default selection is requested.</param>
    /// <param name="secondSize">The genome size set through the retained bounds stage.</param>
    [Theory]
    [InlineData(2, 3)]
    [InlineData(3, 2)]
    public async Task DefaultSelectionUsesEachBuildsGenomeSize(int firstSize, int secondSize)
    {
        var firstPoint = Enumerable.Range(1, firstSize).Select(i => (double)i).ToArray();
        var secondPoint = Enumerable.Range(7, secondSize).Select(i => (double)i).ToArray();
        var bounds = DifferentialEvolutionBuilder.ForFunction(new SphereEvaluator());
        var builder = bounds.WithBounds(firstPoint, firstPoint)
            .WithPopulationSize(8)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .WithSeed(17);

        using var first = builder.Build();
        _ = bounds.WithBounds(secondPoint, secondPoint);
        using var second = builder.Build();

        await AssertSurvivorsAsync(second, secondPoint).ConfigureAwait(true);
        await AssertSurvivorsAsync(first, firstPoint).ConfigureAwait(true);
    }

    private static async Task AssertSurvivorsAsync(DifferentialEvolution optimizer, double[] point)
    {
        var population = await optimizer.RunAsync().WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(true);
        Assert.Equal(1, population.GenerationNumber);
        Assert.Equal(point.Length, population.GenomeSize);
        for (var i = 0; i < population.PopulationSize; i++)
        {
            population.MoveCursorTo(i);
            Assert.Equal(point, population.IndividualCursor.Genes.ToArray());
        }
    }
}
