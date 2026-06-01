using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end convergence for each constant-parameter mutation strategy exposed by the
/// builder. Replaces the old single-tolerance=5.0 test: each strategy must reach a tight
/// tolerance on the (unimodal) Sphere function.
/// </summary>
[Trait("Category", "Integration")]
public class MutationStrategyConvergenceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("best/1")]
    [InlineData("current-to-best/1")]
    [InlineData("rand/2")]
    [InlineData("best/2")]
    [InlineData("dithered-best/1")]
    public async Task EachStrategyConvergesOnSphere(
        string strategy)
    {
        var evaluator = new SphereEvaluator(dimension: 5);

        var best = await BuilderOptimizer.BestOfAsync(attempts: 3, Timeout, () => Build(strategy, evaluator));

        ConvergenceAssert.ReachedOptimum(evaluator, best, valueTolerance: 1e-3);
    }

    private static DotNetDifferentialEvolution.DifferentialEvolution Build(
        string strategy,
        SphereEvaluator evaluator)
    {
        var withSampling = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(60)
            .WithUniformPopulationSampling();

        ISelectionStrategyRequired afterMutation = strategy switch
        {
            "best/1" => withSampling.WithBestMutationStrategy(0.5, 0.9),
            "current-to-best/1" => withSampling.WithCurrentToBestMutationStrategy(0.5, 0.9),
            "rand/2" => withSampling.WithRandTwoMutationStrategy(0.5, 0.9),
            "best/2" => withSampling.WithBestTwoMutationStrategy(0.5, 0.9),
            "dithered-best/1" => withSampling.WithMutationStrategy(
                new BestMutationStrategy(), new DitheredControlParameterProvider(0.3, 0.9, 0.9)),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
        };

        return afterMutation
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new StagnationStreakTerminationStrategy(2000, 1e-12))
            .UseProcessors(1)
            .Build();
    }
}
