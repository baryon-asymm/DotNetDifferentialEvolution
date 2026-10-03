using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Common.Helpers;

namespace DotNetDifferentialEvolution.UnitTests.AlgorithmExecutors;

/// <summary>
/// The executor's own guard against a strategy that reads F and CR from the context paired with
/// a context that supplies neither. The builder refuses that pairing before it reaches the
/// executor (<c>UnitTests/Builder</c>), so these tests assemble the context by hand: the one path
/// on which this guard is the only one. Unguarded, the run is silently wrong — NaN parameters,
/// NaN trials, every trial rejected, the initial sample reported as the optimum.
/// </summary>
[Trait("Category", "Unit")]
public class AlgorithmExecutorTests
{
    private const int PopulationSize = 10;

    /// <summary>
    /// The type names of the strategies that read F and CR from the context, one theory row each.
    /// The rows carry a name rather than the strategy itself because a string is serializable, so
    /// Test Explorer can list every row; <see cref="CreateStrategy"/> builds the instance.
    /// </summary>
    /// <returns>The strategy names.</returns>
    public static TheoryData<string> StrategiesNeedingControlParameters() =>
    [
        nameof(RandMutationStrategy),
        nameof(BestMutationStrategy),
        nameof(RandTwoMutationStrategy)
    ];

    /// <summary>
    /// The executor refuses a strategy that reads F and CR from the context when the context it is
    /// given has no control-parameter provider.
    /// </summary>
    /// <param name="strategyName">The type name of the strategy under test.</param>
    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void AHandBuiltContextWithoutAProviderIsRefused(
        string strategyName)
    {
        var mutationStrategy = CreateStrategy(strategyName);
        var context = CreateContext(controlParameterProvider: null);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new AlgorithmExecutor(mutationStrategy, new SelectionStrategy(context.GenomeSize), context));

        Assert.Contains("control-parameter provider", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same strategies are accepted once the context supplies a control-parameter provider.
    /// </summary>
    /// <param name="strategyName">The type name of the strategy under test.</param>
    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void TheSameStrategyIsAcceptedWhenTheContextHasAProvider(
        string strategyName)
    {
        var mutationStrategy = CreateStrategy(strategyName);
        var context = CreateContext(new ConstantControlParameterProvider(0.5, 0.9));

        var executor = new AlgorithmExecutor(mutationStrategy, new SelectionStrategy(context.GenomeSize), context);

        Assert.NotNull(executor);
    }

    /// <summary>
    /// The legacy strategy takes F and CR through its constructor, so the executor accepts it with a
    /// context that has no provider.
    /// </summary>
    [Fact]
    public void AStrategyThatCarriesItsOwnParametersNeedsNoProvider()
    {
        var context = CreateContext(controlParameterProvider: null);
        var legacy = new MutationStrategy(
            mutationForce: 0.5,
            crossoverProbability: 0.9);

        var executor = new AlgorithmExecutor(legacy, new SelectionStrategy(context.GenomeSize), context);

        Assert.NotNull(executor);
    }

    private static IMutationStrategy CreateStrategy(
        string strategyName) => strategyName switch
        {
            nameof(RandMutationStrategy) => new RandMutationStrategy(),
            nameof(BestMutationStrategy) => new BestMutationStrategy(),
            nameof(RandTwoMutationStrategy) => new RandTwoMutationStrategy(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategyName))
        };

    private static ProblemContext CreateContext(IControlParameterProvider? controlParameterProvider) =>
        ProblemContextHelper.CreateContext(
            PopulationSize,
            new SphereEvaluator(dimension: 2),
            new LimitGenerationNumberTerminationStrategy(1),
            controlParameterProvider,
            seed: 1);
}
