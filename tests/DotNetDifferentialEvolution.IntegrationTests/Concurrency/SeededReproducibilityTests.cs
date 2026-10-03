using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.Concurrency;

/// <summary>
/// A seeded run must be bit-reproducible, including in parallel. The striping is deterministic —
/// worker <c>k</c> always handles individuals <c>{k, k+W, …}</c> and each individual is built,
/// evaluated and selected end-to-end by that one worker — so no floating-point result depends on
/// how the workers interleave. Giving each worker its own generator therefore makes a multi-worker
/// run as reproducible as a single-worker one.
/// </summary>
/// <remarks>
/// Reproducibility holds <em>for a given worker count</em>. Individual <c>i</c> draws from
/// worker <c>i mod W</c>'s stream, so changing <c>W</c> changes which numbers each individual
/// sees; that is a property of per-worker streams, not a defect, and it is documented on
/// <c>WithSeed</c>.
/// </remarks>
[Trait("Category", "Integration")]
public class SeededReproducibilityTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Two runs with the same seed and worker count report bit-identical best value, best genes and
    /// population fitness values.
    /// </summary>
    /// <param name="workers">The number of worker threads both runs use.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task TheSameSeedReproducesTheRunExactly(
        int workers)
    {
        var first = await RunAsync(seed: 20260728, workers: workers).ConfigureAwait(true);
        var second = await RunAsync(seed: 20260728, workers: workers).ConfigureAwait(true);

        Assert.Equal(first.BestFfValue, second.BestFfValue);
        Assert.Equal(first.BestGenes, second.BestGenes);
        Assert.Equal(first.PopulationFfValues, second.PopulationFfValues);
    }

    /// <summary>
    /// With zero generations the run reports the sampled initial population, and the same seed
    /// samples it identically.
    /// </summary>
    /// <param name="workers">The number of worker threads both runs use.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task TheSameSeedReproducesTheInitialPopulationToo(
        int workers)
    {
        var first = await RunAsync(seed: 7, workers: workers, generations: 0).ConfigureAwait(true);
        var second = await RunAsync(seed: 7, workers: workers, generations: 0).ConfigureAwait(true);

        Assert.Equal(first.PopulationFfValues, second.PopulationFfValues);
    }

    /// <summary>
    /// Two different seeds lead to different final populations.
    /// </summary>
    /// <param name="workers">The number of worker threads both runs use.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task DifferentSeedsProduceDifferentRuns(
        int workers)
    {
        var first = await RunAsync(seed: 1, workers: workers).ConfigureAwait(true);
        var second = await RunAsync(seed: 2, workers: workers).ConfigureAwait(true);

        Assert.NotEqual(first.PopulationFfValues, second.PopulationFfValues);
    }

    /// <summary>
    /// Without <c>WithSeed</c> two runs differ, so the default builder chain stays unseeded.
    /// </summary>
    [Fact]
    public async Task AnUnseededRunIsStillFreeToDiffer()
    {
        // The default must stay unseeded; otherwise every run of a program would follow the same
        // trajectory, which is not what an unqualified builder chain promises.
        var first = await RunAsync(seed: null, workers: 4).ConfigureAwait(true);
        var second = await RunAsync(seed: null, workers: 4).ConfigureAwait(true);

        Assert.NotEqual(first.PopulationFfValues, second.PopulationFfValues);
    }

    /// <summary>
    /// A seeded JADE run, which also draws from the control-parameter provider and from the
    /// orchestrator's archive eviction, reproduces its best value and population exactly.
    /// </summary>
    /// <param name="workers">The number of worker threads both runs use.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task AnAdaptiveVariantIsReproducibleIncludingItsArchiveEviction(
        int workers)
    {
        // JADE draws from three separate streams: the workers' (mutation and crossover), the
        // control-parameter provider's (which is handed a worker's provider), and the
        // orchestrator's own (random archive eviction once the archive is full).
        var first = await RunAsync(seed: 99, workers: workers, configure: builder => builder.WithJade()).ConfigureAwait(true);
        var second = await RunAsync(seed: 99, workers: workers, configure: builder => builder.WithJade()).ConfigureAwait(true);

        Assert.Equal(first.BestFfValue, second.BestFfValue);
        Assert.Equal(first.PopulationFfValues, second.PopulationFfValues);
    }

    private static async Task<RunResult> RunAsync(
        int? seed,
        int workers,
        int generations = 40,
        Func<IMutationStrategyRequired, ITerminationConditionRequired>? configure = null)
    {
        var evaluator = new SphereEvaluator(dimension: 6);

        var configured = configure is null
            ? DifferentialEvolutionBuilder.ForFunction(evaluator)
                .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                .WithPopulationSize(40)
                .WithUniformPopulationSampling()
                .WithDefaultMutationStrategy(0.6, 0.9)
                .WithDefaultSelectionStrategy()
            : configure(
                DifferentialEvolutionBuilder.ForFunction(evaluator)
                    .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
                    .WithPopulationSize(40)
                    .WithUniformPopulationSampling());

        var builder = configured
            // A generation count of zero terminates before the first generation is evolved, so
            // what the run reports is the sampled initial population.
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(generations))
            .UseProcessors(workers);

        using var de = (seed is { } seedValue ? builder.WithSeed(seedValue) : builder).Build();

        var population = await de.RunAsync().WaitAsync(Timeout).ConfigureAwait(true);

        return RunResult.From(population);
    }

    private sealed record RunResult(
        double BestFfValue,
        double[] BestGenes,
        double[] PopulationFfValues)
    {
        public static RunResult From(
            Population population)
        {
            population.MoveCursorToBestIndividual();

            var ffValues = new double[population.PopulationSize];
            for (var i = 0; i < ffValues.Length; i++)
            {
                population.MoveCursorTo(i);
                ffValues[i] = population.IndividualCursor.FitnessFunctionValue;
            }

            population.MoveCursorToBestIndividual();

            return new RunResult(
                population.IndividualCursor.FitnessFunctionValue,
                population.IndividualCursor.Genes.ToArray(),
                ffValues);
        }
    }
}
