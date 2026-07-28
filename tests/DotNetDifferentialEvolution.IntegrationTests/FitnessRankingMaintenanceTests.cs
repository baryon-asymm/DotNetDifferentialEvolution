using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.LocalSearch;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Variants;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// <c>DE/current-to-pbest/1</c> draws <c>x_pbest</c> from the top <c>p%</c> of
/// <see cref="ProblemContext.FitnessSortedIndices"/>. That ranking used to be built once in
/// <c>Build()</c> and refreshed only by the adaptive generation strategies, so a hand-wired
/// current-to-pbest run kept drawing its "best" individuals from the ranking of the *initial
/// random population* for the whole run. The engine now maintains the ranking whenever the
/// configured mutation strategy declares it needs one, independently of which generation strategy
/// — if any — is installed.
/// </summary>
/// <remarks>
/// The property asserted is the ranking's defining one rather than a golden ordering: the fitness
/// values read through the ranking must be non-decreasing, and the ranked indices must be a
/// permutation of the active population. That is tie-tolerant and independent of the sort's
/// stability.
/// </remarks>
[Trait("Category", "Integration")]
public class FitnessRankingMaintenanceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task AHandWiredCurrentToPBestRunKeepsTheRankingCurrent()
    {
        var spy = await RunAsync(builder => builder
            .WithMutationStrategy(
                new CurrentToPBestMutationStrategy(0.1),
                new ConstantControlParameterProvider(0.5, 0.9))
            .WithDefaultSelectionStrategy());

        AssertRankingWasCorrectEveryGeneration(spy);
        Assert.True(
            spy.RankingEverChanged,
            "the ranking never changed, so it was still the one computed at generation 0");
    }

    [Fact]
    public async Task AThirdPartyGenerationStrategyDoesNotHaveToMaintainTheRankingItself()
    {
        // Being present must not make a generation strategy responsible for the shared ranking:
        // this one only counts generations, and the p-best strategy still gets a current ranking.
        var generationStrategy = new CountingGenerationStrategy();

        var spy = await RunAsync(builder => builder.WithVariant(
            new ObservedPBestVariant(generationStrategy)));

        AssertRankingWasCorrectEveryGeneration(spy);
        Assert.True(spy.RankingEverChanged);
        Assert.Equal(spy.Snapshots.Count, generationStrategy.Generations);
    }

    [Theory]
    [InlineData("jade")]
    [InlineData("shade")]
    public async Task TheAdaptiveVariantsStillRankCorrectly(
        string variant)
    {
        var spy = await RunAsync(builder => variant == "jade"
            ? builder.WithJade()
            : builder.WithShade());

        AssertRankingWasCorrectEveryGeneration(spy);
        Assert.True(spy.RankingEverChanged, $"{variant} produced a frozen ranking");
    }

    [Fact]
    public async Task AHandWiredCurrentToPBestRunConvergesLikeTheAdaptiveOnes()
    {
        // The end-to-end symptom of the frozen ranking: the hand-wired run stalled around 1E-03
        // on a 5-D sphere while JADE reached 1E-29. Constant F/CR converges more slowly than
        // adaptive F/CR, so the bar here is only that the run is genuinely optimizing.
        var evaluator = new SphereEvaluator(dimension: 5);

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(40)
            .WithUniformPopulationSampling()
            .WithMutationStrategy(
                new CurrentToPBestMutationStrategy(0.1),
                new ConstantControlParameterProvider(0.5, 0.9))
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(300))
            .UseProcessors(1)
            .Build();

        var result = await de.RunAsync().WaitAsync(Timeout);

        result.MoveCursorToBestIndividual();
        Assert.True(
            result.IndividualCursor.FitnessFunctionValue < 1E-10,
            $"best was {result.IndividualCursor.FitnessFunctionValue}");
    }

    private static async Task<RankingSpy> RunAsync(
        Func<IMutationStrategyRequired, ITerminationConditionRequired> configure)
    {
        var evaluator = new SphereEvaluator(dimension: 5);
        var spy = new RankingSpy();

        using var de = configure(
                DifferentialEvolutionBuilder.ForFunction(evaluator)
                    .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                    .WithPopulationSize(40)
                    .WithUniformPopulationSampling())
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(60))
            .UseProcessors(1)
            .WithLocalSearch(spy)
            .Build();

        await de.RunAsync().WaitAsync(Timeout);

        return spy;
    }

    private static void AssertRankingWasCorrectEveryGeneration(
        RankingSpy spy)
    {
        Assert.NotEmpty(spy.Snapshots);

        foreach (var (generationNumber, ranking, ffValues) in spy.Snapshots)
        {
            Assert.Equal(
                Enumerable.Range(0, ranking.Length).ToArray(),
                ranking.Order().ToArray());

            var ranked = ranking.Select(index => ffValues[index]).ToArray();
            for (int k = 1; k < ranked.Length; k++)
            {
                Assert.True(
                    ranked[k - 1] <= ranked[k],
                    $"generation {generationNumber}: rank {k - 1} holds {ranked[k - 1]} "
                    + $"but rank {k} holds {ranked[k]}");
            }
        }
    }

    /// <summary>
    /// Snapshots the ranking and the fitness values it ranks at the end of every generation. It
    /// runs on the orchestrator thread after the generation strategy, which is exactly where a
    /// consumer of the ranking would read it.
    /// </summary>
    private sealed class RankingSpy : ILocalSearchRefiner
    {
        private int[]? _previousRanking;

        public List<(int GenerationNumber, int[] Ranking, double[] FfValues)> Snapshots { get; } = [];

        public bool RankingEverChanged { get; private set; }

        public void Refine(
            ProblemContext context,
            int generationNumber)
        {
            ArgumentNullException.ThrowIfNull(context);

            var activeSize = context.CurrentPopulationSize;
            var ranking = context.FitnessSortedIndices.Span.Slice(0, activeSize).ToArray();
            var ffValues = context.CurrentPopulation.FfValues.Span.Slice(0, activeSize).ToArray();

            if (_previousRanking is not null && ranking.SequenceEqual(_previousRanking) == false)
                RankingEverChanged = true;
            _previousRanking = ranking;

            Snapshots.Add((generationNumber, ranking, ffValues));
        }
    }

    /// <summary>
    /// A third-party variant: <c>DE/current-to-pbest/1</c> with fixed control parameters and a
    /// generation hook that adapts nothing.
    /// </summary>
    private sealed class ObservedPBestVariant : IDeVariant
    {
        private readonly IGenerationStrategy _generationStrategy;

        public ObservedPBestVariant(
            IGenerationStrategy generationStrategy)
            => _generationStrategy = generationStrategy;

        public DeVariantSetup Configure(
            in DeVariantConfiguration configuration)
            => new()
            {
                MutationStrategy = new CurrentToPBestMutationStrategy(0.1),
                ControlParameterProvider = new ConstantControlParameterProvider(0.5, 0.9),
                GenerationStrategy = _generationStrategy
            };
    }

    /// <summary>An observer-only generation strategy: it adapts nothing and ranks nothing.</summary>
    private sealed class CountingGenerationStrategy : IGenerationStrategy
    {
        public int Generations { get; private set; }

        public void AfterGeneration(
            GenerationContext context,
            ReadOnlySpan<TrialRecord> trialRecords)
            => Generations++;
    }
}
