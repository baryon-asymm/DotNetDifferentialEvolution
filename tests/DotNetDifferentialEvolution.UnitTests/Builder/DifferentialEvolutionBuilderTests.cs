using System.Reflection;
using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.LocalSearch;
using DotNetDifferentialEvolution.Models;
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

    [Fact]
    public void WithLocalSearch_ThrowsWhenRefinerIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(1)
                .WithLocalSearch(null!));
    }

    [Fact]
    public void WithLocalSearch_ThrowsWhenIntervalIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(1)
                .WithLocalSearch(new NoOpRefiner(), everyNGenerations: 0));
    }

    // The initial archive capacity is round(archiveSizeRate * populationSize). 0.5 * 17 = 8.5 is
    // an exact midpoint: the papers round half away from zero, .NET's default
    // MidpointRounding.ToEven would round it down to 8.
    [Fact]
    public void WithJade_RoundsAMidpointArchiveCapacityHalfUp()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
            .WithPopulationSize(17)
            .WithUniformPopulationSampling()
            .WithJade(archiveSizeRate: 0.5)
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.Equal(9, GetArchiveCapacity(de));
    }

    [Fact]
    public void WithShade_RoundsAMidpointArchiveCapacityHalfUp()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
            .WithPopulationSize(17)
            .WithUniformPopulationSampling()
            .WithShade(archiveSizeRate: 0.5)
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.Equal(9, GetArchiveCapacity(de));
    }

    [Fact]
    public void WithLShade_RoundsAMidpointArchiveCapacityHalfUp()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 })
            .WithPopulationSize(17)
            .WithUniformPopulationSampling()
            .WithLShade(maxEvaluationNumber: 1000, archiveSizeRate: 0.5)
            .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(1000))
            .UseProcessors(1)
            .Build();

        Assert.Equal(9, GetArchiveCapacity(de));
    }

    /// <summary>
    /// Reads the archive capacity the builder installed on the problem context. The context is
    /// deliberately not exposed on <see cref="DifferentialEvolution"/>, so it is read
    /// reflectively rather than by widening the public API for a test's sake.
    /// </summary>
    private static int GetArchiveCapacity(
        DifferentialEvolution differentialEvolution)
    {
        var field = typeof(DifferentialEvolution).GetField(
                        "_problemContext", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "DifferentialEvolution._problemContext was renamed; update this test.");

        return Assert.IsType<ProblemContext>(field.GetValue(differentialEvolution)).ArchiveCapacity;
    }

    private sealed class NoOpRefiner : ILocalSearchRefiner
    {
        public void Refine(ProblemContext context, int generationNumber)
        {
            // Intentionally does nothing; used only to satisfy the non-null refiner argument.
        }
    }
}
