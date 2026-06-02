using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.UnitTests.Builder;

/// <summary>
/// Tests the fluent builder's argument validation and that a fully-configured chain produces
/// a usable instance. The staged fluent interfaces already enforce configuration order at
/// compile time; these tests cover the runtime value guards.
/// </summary>
[Trait("Category", "Unit")]
public class DifferentialEvolutionBuilderTests
{
    private static SphereEvaluator Evaluator => new(dimension: 2);

    [Fact]
    public void WithBounds_ThrowsWhenLengthsDiffer()
    {
        Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0 }));
    }

    [Fact]
    public void WithBounds_ThrowsWhenLowerExceedsUpper()
    {
        Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 5.0, 0.0 }, new[] { 1.0, 1.0 }));
    }

    [Fact]
    public void WithPopulationSize_ThrowsWhenNotPositive()
    {
        Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0 }, new[] { 1.0 })
                .WithPopulationSize(0));
    }

    [Fact]
    public void UseProcessors_ThrowsWhenNotPositive()
    {
        Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0 }, new[] { 1.0 })
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(0));
    }

    [Fact]
    public void WithJade_ThrowsWhenArchiveSizeRateIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithJade(archiveSizeRate: -1.0));
    }

    [Fact]
    public void WithLShade_ThrowsWhenEvaluationBudgetIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithLShade(maxEvaluationNumber: 0));
    }

    [Fact]
    public void WithLShade_ThrowsWhenTerminationEvaluationBudgetDoesNotMatch()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithLShade(maxEvaluationNumber: 1000)
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(2000))
                .UseProcessors(1)
                .Build());
    }

    [Fact]
    public void WithLShade_BuildsWhenTerminationEvaluationBudgetMatches()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
            .WithPopulationSize(10)
            .WithUniformPopulationSampling()
            .WithLShade(maxEvaluationNumber: 1000)
            .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(1000))
            .UseProcessors(1)
            .Build();

        Assert.NotNull(de);
    }

    [Fact]
    public void Build_ThrowsWhenPopulationIsTooSmallForTheMutationStrategy()
    {
        // DE/rand/2 draws five distinct individuals plus the target, so it needs at least six.
        Assert.Throws<InvalidOperationException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
                .WithPopulationSize(5)
                .WithUniformPopulationSampling()
                .WithRandTwoMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(1)
                .Build());
    }

    [Fact]
    public void Build_WithCompleteConfiguration_ProducesAUsableInstance()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(new[] { -5.0, -5.0 }, new[] { 5.0, 5.0 })
            .WithPopulationSize(20)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.NotNull(de);
    }
}
