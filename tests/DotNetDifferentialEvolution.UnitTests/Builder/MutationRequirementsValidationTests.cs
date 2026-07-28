using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.UnitTests.Builder;

/// <summary>
/// A mutation strategy declares what the engine must provision for it. The builder has to act on
/// that declaration rather than let an unsatisfied requirement decay into a silently wrong run:
/// a strategy that reads F and CR from the <see cref="MutationContext"/> and is given no
/// <see cref="IControlParameterProvider"/> receives NaN for both, which turns every trial vector
/// into NaN and every selection into a rejection. The run then completes normally and reports the
/// best of the initial random sample as its answer.
/// </summary>
[Trait("Category", "Unit")]
public class MutationRequirementsValidationTests
{
    private static SphereEvaluator Evaluator => new(dimension: 2);

    public static TheoryData<IMutationStrategy> StrategiesNeedingControlParameters() =>
        new()
        {
            new RandMutationStrategy(),
            new BestMutationStrategy(),
            new CurrentToBestMutationStrategy(),
            new RandTwoMutationStrategy(),
            new BestTwoMutationStrategy(),
            new CurrentToPBestMutationStrategy(0.1)
        };

    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider(
        IMutationStrategy mutationStrategy)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Build(builder => builder.WithMutationStrategy(mutationStrategy)));

        Assert.Contains("control", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void BuildSucceedsWhenTheSameStrategyIsPairedWithAProvider(
        IMutationStrategy mutationStrategy)
    {
        using var de = Build(builder => builder.WithMutationStrategy(
            mutationStrategy, new ConstantControlParameterProvider(0.5, 0.9)));

        Assert.NotNull(de);
    }

    [Fact]
    public void EveryStrategyNeedingControlParametersSaysSo()
    {
        foreach (var mutationStrategy in StrategiesNeedingControlParameters()
                     .Select(row => (IMutationStrategy)row[0]))
        {
            Assert.True(
                mutationStrategy.Requirements.HasFlag(MutationRequirements.ControlParameters),
                $"{mutationStrategy.GetType().Name} reads F/CR from the context but does not declare it.");
        }
    }

    [Fact]
    public void TheLegacyStrategyCarriesItsOwnParametersAndStillBuildsAlone()
    {
        // MutationStrategy takes F and CR through its constructor and ignores the context, so it
        // is the one built-in that must keep working without a provider.
        var legacy = new MutationStrategy(
            mutationForce: 0.5,
            crossoverProbability: 0.9,
            populationSize: 10,
            lowerBound: new[] { 0.0, 0.0 },
            upperBound: new[] { 1.0, 1.0 });

        Assert.Equal(MutationRequirements.None, legacy.Requirements);

        using var de = Build(builder => builder.WithMutationStrategy(legacy));

        Assert.NotNull(de);
    }

    [Fact]
    public void ACustomStrategyThatDeclaresNoRequirementsBuildsAlone()
    {
        using var de = Build(builder => builder.WithMutationStrategy(new SelfContainedMutationStrategy()));

        Assert.NotNull(de);
    }

    [Fact]
    public void TheCurrentToPBestStrategyDeclaresTheRankingAndArchiveItReads()
    {
        var requirements = new CurrentToPBestMutationStrategy(0.1).Requirements;

        Assert.True(requirements.HasFlag(MutationRequirements.FitnessRanking));
        Assert.True(requirements.HasFlag(MutationRequirements.Archive));
    }

    [Theory]
    [InlineData("jde")]
    [InlineData("jade")]
    [InlineData("shade")]
    [InlineData("lshade")]
    public void ThePresetVariantsSatisfyTheirOwnStrategysRequirements(
        string variant)
    {
        var evaluator = Evaluator;
        var builder = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(10)
            .WithUniformPopulationSampling();

        var configured = variant switch
        {
            "jde" => builder.WithJde(),
            "jade" => builder.WithJade(),
            "shade" => builder.WithShade(),
            "lshade" => builder.WithLShade(maxEvaluationNumber: 100),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };

        using var de = configured
            .WithTerminationCondition(variant == "lshade"
                ? new LimitEvaluationNumberTerminationStrategy(100)
                : new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        Assert.NotNull(de);
    }

    private static DifferentialEvolution Build(
        Func<IMutationStrategyRequired, ISelectionStrategyRequired> configureMutation)
    {
        var evaluator = Evaluator;

        return configureMutation(
                DifferentialEvolutionBuilder.ForFunction(evaluator)
                    .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                    .WithPopulationSize(10)
                    .WithUniformPopulationSampling())
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();
    }

    /// <summary>A strategy that needs nothing from the engine: it copies the parent through.</summary>
    private sealed class SelfContainedMutationStrategy : IMutationStrategy
    {
        public MutationRequirements Requirements => MutationRequirements.None;

        public void Mutate(
            in MutationContext context)
        {
            context.Population
                .Slice(context.IndividualIndex * context.GenomeSize, context.GenomeSize)
                .CopyTo(context.TrialIndividual);
        }
    }
}
