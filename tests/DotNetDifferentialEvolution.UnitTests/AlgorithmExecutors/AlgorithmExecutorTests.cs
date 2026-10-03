using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

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

    public static TheoryData<IMutationStrategy> StrategiesNeedingControlParameters() =>
        new()
        {
            new RandMutationStrategy(),
            new BestMutationStrategy(),
            new RandTwoMutationStrategy()
        };

    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void AHandBuiltContextWithoutAProviderIsRefused(
        IMutationStrategy mutationStrategy)
    {
        var context = CreateContext(controlParameterProvider: null);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new AlgorithmExecutor(mutationStrategy, new SelectionStrategy(context.GenomeSize), context));

        Assert.Contains("control-parameter provider", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(StrategiesNeedingControlParameters))]
    public void TheSameStrategyIsAcceptedWhenTheContextHasAProvider(
        IMutationStrategy mutationStrategy)
    {
        var context = CreateContext(new ConstantControlParameterProvider(0.5, 0.9));

        var executor = new AlgorithmExecutor(mutationStrategy, new SelectionStrategy(context.GenomeSize), context);

        Assert.NotNull(executor);
    }

    [Fact]
    public void AStrategyThatCarriesItsOwnParametersNeedsNoProvider()
    {
        var context = CreateContext(controlParameterProvider: null);
        var legacy = new MutationStrategy(
            mutationForce: 0.5,
            crossoverProbability: 0.9,
            populationSize: PopulationSize,
            lowerBound: context.GenesLowerBound,
            upperBound: context.GenesUpperBound);

        var executor = new AlgorithmExecutor(legacy, new SelectionStrategy(context.GenomeSize), context);

        Assert.NotNull(executor);
    }

    private static ProblemContext CreateContext(IControlParameterProvider? controlParameterProvider) =>
        ProblemContextHelper.CreateContext(
            PopulationSize,
            new SphereEvaluator(dimension: 2),
            new LimitGenerationNumberTerminationStrategy(1),
            controlParameterProvider,
            seed: 1);
}
