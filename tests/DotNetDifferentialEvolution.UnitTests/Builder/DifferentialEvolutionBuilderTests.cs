using System.Reflection;
using DotNetDifferentialEvolution.LocalSearch;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.UnitTests.Builder;

/// <summary>
/// Tests the fluent builder's argument validation and that a fully-configured chain produces
/// a usable instance. The staged fluent interfaces already enforce configuration order at
/// compile time; these tests cover the runtime value guards.
/// </summary>
[Trait("Category", "Unit")]
public class DifferentialEvolutionBuilderTests
{
    // Bounds shared by the tests below. The builder only reads them (it takes ReadOnlyMemory), so
    // one instance of each serves every test.
    private static readonly double[] OneLowerBound = [0.0];
    private static readonly double[] OneUpperBound = [1.0];
    private static readonly double[] TwoLowerBounds = [0.0, 0.0];
    private static readonly double[] TwoUpperBounds = [1.0, 1.0];
    private static readonly double[] LowerBoundsAboveUpperInFirstDimension = [5.0, 0.0];
    private static readonly double[] WideLowerBounds = [-5.0, -5.0];
    private static readonly double[] WideUpperBounds = [5.0, 5.0];

    private static SphereEvaluator Evaluator => new(dimension: 2);

    /// <summary>
    /// Lower and upper bounds of different lengths are rejected.
    /// </summary>
    [Fact]
    public void WithBoundsThrowsWhenLengthsDiffer()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, OneUpperBound));
    }

    /// <summary>
    /// A lower bound above its upper bound in any dimension is rejected.
    /// </summary>
    [Fact]
    public void WithBoundsThrowsWhenLowerExceedsUpper()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(LowerBoundsAboveUpperInFirstDimension, TwoUpperBounds));
    }

    /// <summary>
    /// A population size of zero is rejected.
    /// </summary>
    [Fact]
    public void WithPopulationSizeThrowsWhenNotPositive()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(OneLowerBound, OneUpperBound)
                .WithPopulationSize(0));
    }

    /// <summary>
    /// A processor count of zero is rejected.
    /// </summary>
    [Fact]
    public void UseProcessorsThrowsWhenNotPositive()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(OneLowerBound, OneUpperBound)
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(0));
    }

    /// <summary>
    /// The JADE preset rejects a negative archive size rate.
    /// </summary>
    [Fact]
    public void WithJadeThrowsWhenArchiveSizeRateIsNegative()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, TwoUpperBounds)
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithJade(archiveSizeRate: -1.0));
    }

    /// <summary>
    /// The L-SHADE preset rejects an evaluation budget of zero.
    /// </summary>
    [Fact]
    public void WithLShadeThrowsWhenEvaluationBudgetIsNotPositive()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, TwoUpperBounds)
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithLShade(maxEvaluationNumber: 0));
    }

    /// <summary>
    /// L-SHADE plans its population reduction against its evaluation budget, so a build whose
    /// evaluation-limit termination disagrees with that budget is refused.
    /// </summary>
    [Fact]
    public void WithLShadeThrowsWhenTerminationEvaluationBudgetDoesNotMatch()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, TwoUpperBounds)
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithLShade(maxEvaluationNumber: 1000)
                .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(2000))
                .UseProcessors(1)
                .Build());
    }

    /// <summary>
    /// L-SHADE builds when the evaluation-limit termination matches its evaluation budget.
    /// </summary>
    [Fact]
    public void WithLShadeBuildsWhenTerminationEvaluationBudgetMatches()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(TwoLowerBounds, TwoUpperBounds)
            .WithPopulationSize(10)
            .WithUniformPopulationSampling()
            .WithLShade(maxEvaluationNumber: 1000)
            .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(1000))
            .UseProcessors(1)
            .Build();

        Assert.NotNull(de);
    }

    /// <summary>
    /// DE/rand/2 needs at least six individuals, so a population of five is refused at build time.
    /// </summary>
    [Fact]
    public void BuildThrowsWhenPopulationIsTooSmallForTheMutationStrategy()
    {
        // DE/rand/2 draws five distinct individuals plus the target, so it needs at least six.
        _ = Assert.Throws<InvalidOperationException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, TwoUpperBounds)
                .WithPopulationSize(5)
                .WithUniformPopulationSampling()
                .WithRandTwoMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(1)
                .Build());
    }

    /// <summary>
    /// A fully configured chain builds an instance.
    /// </summary>
    [Fact]
    public void BuildWithCompleteConfigurationProducesAUsableInstance()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(WideLowerBounds, WideUpperBounds)
            .WithPopulationSize(20)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.NotNull(de);
    }

    /// <summary>
    /// A null local-search refiner is rejected.
    /// </summary>
    [Fact]
    public void WithLocalSearchThrowsWhenRefinerIsNull()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, TwoUpperBounds)
                .WithPopulationSize(10)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.5, 0.9)
                .WithDefaultSelectionStrategy()
                .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
                .UseProcessors(1)
                .WithLocalSearch(null!));
    }

    /// <summary>
    /// A local-search interval of zero generations is rejected.
    /// </summary>
    [Fact]
    public void WithLocalSearchThrowsWhenIntervalIsNotPositive()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DifferentialEvolutionBuilder.ForFunction(Evaluator)
                .WithBounds(TwoLowerBounds, TwoUpperBounds)
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
    /// <summary>
    /// JADE's initial archive capacity of 0.5 * 17 = 8.5 is rounded half away from zero to 9.
    /// </summary>
    [Fact]
    public void WithJadeRoundsAMidpointArchiveCapacityHalfUp()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(TwoLowerBounds, TwoUpperBounds)
            .WithPopulationSize(17)
            .WithUniformPopulationSampling()
            .WithJade(archiveSizeRate: 0.5)
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.Equal(9, GetArchiveCapacity(de));
    }

    /// <summary>
    /// SHADE's initial archive capacity of 0.5 * 17 = 8.5 is rounded half away from zero to 9.
    /// </summary>
    [Fact]
    public void WithShadeRoundsAMidpointArchiveCapacityHalfUp()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(TwoLowerBounds, TwoUpperBounds)
            .WithPopulationSize(17)
            .WithUniformPopulationSampling()
            .WithShade(archiveSizeRate: 0.5)
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.Equal(9, GetArchiveCapacity(de));
    }

    /// <summary>
    /// L-SHADE's initial archive capacity of 0.5 * 17 = 8.5 is rounded half away from zero to 9.
    /// </summary>
    [Fact]
    public void WithLShadeRoundsAMidpointArchiveCapacityHalfUp()
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(Evaluator)
            .WithBounds(TwoLowerBounds, TwoUpperBounds)
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
