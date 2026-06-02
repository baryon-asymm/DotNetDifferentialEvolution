using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.EndToEnd;

/// <summary>
/// End-to-end convergence for the self-adaptive variants (jDE, JADE, SHADE, L-SHADE) on the
/// Rosenbrock valley. Replaces the old AdaptiveVariantsTester.
/// </summary>
[Trait("Category", "Integration")]
public class AdaptiveVariantsConvergenceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("jDE")]
    [InlineData("JADE")]
    [InlineData("SHADE")]
    public async Task SelfAdaptiveVariantsConvergeOnRosenbrock(
        string variant)
    {
        var evaluator = new RosenbrockEvaluator(dimension: 2);

        var best = await BuilderOptimizer.BestOfAsync(attempts: 3, Timeout, () =>
        {
            var withSampling = DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(50)
                .WithUniformPopulationSampling();

            ITerminationConditionRequired configured = variant switch
            {
                "jDE" => withSampling.WithJde(),
                "JADE" => withSampling.WithJade(),
                "SHADE" => withSampling.WithShade(memorySize: 20),
                _ => throw new ArgumentOutOfRangeException(nameof(variant)),
            };

            return configured
                .WithTerminationCondition(new StagnationStreakTerminationStrategy(2000, 1e-9))
                .UseProcessors(1)
                .Build();
        });

        ConvergenceAssert.ReachedOptimum(evaluator, best, valueTolerance: 1e-6, geneTolerance: 1e-3);
    }

    [Fact]
    public async Task LShadeConvergesOnRosenbrock()
    {
        var evaluator = new RosenbrockEvaluator(dimension: 2);
        const long maxEvaluations = 200_000;

        var best = await BuilderOptimizer.BestOfAsync(attempts: 3, Timeout, () =>
            DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(50)
                .WithUniformPopulationSampling()
                .WithLShade(maxEvaluations)
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(maxEvaluations))
                .UseProcessors(1)
                .Build());

        ConvergenceAssert.ReachedOptimum(evaluator, best, valueTolerance: 1e-6, geneTolerance: 1e-3);
    }
}
