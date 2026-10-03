using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

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

    private static readonly string[] NamesOfStrategiesNeedingControlParameters =
    [
        nameof(RandMutationStrategy),
        nameof(BestMutationStrategy),
        nameof(CurrentToBestMutationStrategy),
        nameof(RandTwoMutationStrategy),
        nameof(BestTwoMutationStrategy),
        nameof(CurrentToPBestMutationStrategy),
    ];

    /// <summary>
    /// The type names of every built-in strategy that reads F and CR from the context, one theory
    /// row each. The rows carry a name rather than the strategy itself because a string is
    /// serializable, so Test Explorer can list every row; <see cref="CreateStrategy"/> builds the
    /// instance.
    /// </summary>
    /// <returns>The strategy names.</returns>
    public static TheoryData<string> StrategiesNeedingControlParameters() =>
        [.. NamesOfStrategiesNeedingControlParameters];

    private static IMutationStrategy CreateStrategy(
        string strategyName) => strategyName switch
        {
            nameof(RandMutationStrategy) => new RandMutationStrategy(),
            nameof(BestMutationStrategy) => new BestMutationStrategy(),
            nameof(CurrentToBestMutationStrategy) => new CurrentToBestMutationStrategy(),
            nameof(RandTwoMutationStrategy) => new RandTwoMutationStrategy(),
            nameof(BestTwoMutationStrategy) => new BestTwoMutationStrategy(),
            nameof(CurrentToPBestMutationStrategy) => new CurrentToPBestMutationStrategy(0.1),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyName))
        };

    /// <summary>
    /// The builder refuses a strategy that reads F and CR from the context when no provider is paired
    /// with it, and its message names the overload that fixes the configuration.
    /// </summary>
    /// <param name="strategyName">The type name of the strategy under test.</param>
    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider(
        string strategyName)
    {
        var mutationStrategy = CreateStrategy(strategyName);
        var exception = Assert.Throws<InvalidOperationException>(
            () => Build(builder => builder.WithMutationStrategy(mutationStrategy)));

        Assert.Contains("control", exception.Message, StringComparison.OrdinalIgnoreCase);

        // The builder's own guard, not the executor's second one (UnitTests/AlgorithmExecutors):
        // only the builder can name the overload that fixes the configuration.
        Assert.Contains("WithMutationStrategy(strategy, provider)", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same strategies build once they are paired with a control-parameter provider.
    /// </summary>
    /// <param name="strategyName">The type name of the strategy under test.</param>
    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void BuildSucceedsWhenTheSameStrategyIsPairedWithAProvider(
        string strategyName)
    {
        var mutationStrategy = CreateStrategy(strategyName);
        using var de = Build(builder => builder.WithMutationStrategy(
            mutationStrategy, new ConstantControlParameterProvider(0.5, 0.9)));

        Assert.NotNull(de);
    }

    /// <summary>
    /// Every built-in strategy that reads F and CR from the context declares
    /// <see cref="MutationRequirements.ControlParameters"/>.
    /// </summary>
    [Fact]
    public void EveryStrategyNeedingControlParametersSaysSo()
    {
        foreach (var strategyName in NamesOfStrategiesNeedingControlParameters)
        {
            var mutationStrategy = CreateStrategy(strategyName);
            Assert.True(
                mutationStrategy.Requirements.HasFlag(MutationRequirements.ControlParameters),
                $"{mutationStrategy.GetType().Name} reads F/CR from the context but does not declare it.");
        }
    }

    /// <summary>
    /// The legacy strategy declares no requirements and builds without a provider, since it takes F and
    /// CR through its constructor.
    /// </summary>
    [Fact]
    public void TheLegacyStrategyCarriesItsOwnParametersAndStillBuildsAlone()
    {
        // MutationStrategy takes F and CR through its constructor and ignores the context, so it
        // is the one built-in that must keep working without a provider.
        var legacy = new MutationStrategy(
            mutationForce: 0.5,
            crossoverProbability: 0.9);

        Assert.Equal(MutationRequirements.None, legacy.Requirements);

        using var de = Build(builder => builder.WithMutationStrategy(legacy));

        Assert.NotNull(de);
    }

    /// <summary>
    /// A custom strategy that declares no requirements builds without a provider.
    /// </summary>
    [Fact]
    public void ACustomStrategyThatDeclaresNoRequirementsBuildsAlone()
    {
        using var de = Build(builder => builder.WithMutationStrategy(new SelfContainedMutationStrategy()));

        Assert.NotNull(de);
    }

    /// <summary>
    /// DE/current-to-pbest/1 declares that it reads the fitness ranking and the archive.
    /// </summary>
    [Fact]
    public void TheCurrentToPBestStrategyDeclaresTheRankingAndArchiveItReads()
    {
        var requirements = new CurrentToPBestMutationStrategy(0.1).Requirements;

        Assert.True(requirements.HasFlag(MutationRequirements.FitnessRanking));
        Assert.True(requirements.HasFlag(MutationRequirements.Archive));
    }

    /// <summary>
    /// Each preset variant builds, which shows that it provisions everything its own strategy requires.
    /// </summary>
    /// <param name="variant">The preset under test.</param>
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
