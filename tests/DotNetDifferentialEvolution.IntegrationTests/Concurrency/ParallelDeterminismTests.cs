using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.IntegrationTests.TestSupport;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.Concurrency;

/// <summary>
/// Concurrency correctness: many workers share the population, trial, fitness, and trial-record
/// buffers. A data race (torn write / lost update) would corrupt the result, so these tests
/// run parallel optimizations repeatedly and require every run to reach the optimum. Repetition
/// turns a rare interleaving bug into a reliable failure.
/// </summary>
[Trait("Category", "Integration")]
public class ParallelDeterminismTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Deliberately generous: the oversubscribed run is a correctness assertion (it must finish
    // and be right), not a stopwatch. A tight bound would be a flaky timing test on shared CI.
    private static readonly TimeSpan OversubscribedTimeout = TimeSpan.FromSeconds(120);

    private const double ValueTolerance = 1e-3;

    [Fact]
    public async Task SingleWorkerAndMultiWorkerBothConverge()
    {
        var evaluator = new SphereEvaluator(dimension: 6);

        var single = await RunAsync(evaluator, workers: 1);
        var multi = await RunAsync(evaluator, workers: Math.Max(2, Environment.ProcessorCount));

        ConvergenceAssert.ReachedOptimum(evaluator, single, ValueTolerance);
        ConvergenceAssert.ReachedOptimum(evaluator, multi, ValueTolerance);
    }

    [Fact]
    public async Task RepeatedParallelRunsAllConverge_NoDataRaceCorruption()
    {
        var evaluator = new SphereEvaluator(dimension: 6);
        var workers = Math.Max(2, Environment.ProcessorCount);

        for (int run = 0; run < 25; run++)
        {
            var result = await RunAsync(evaluator, workers);
            ConvergenceAssert.ReachedOptimum(evaluator, result, ValueTolerance);
        }
    }

    /// <summary>
    /// Oversubscription: more worker threads than the machine has cores, so at every generation
    /// barrier some worker is waiting while another still needs a core to finish its stripe.
    /// The barrier wait must therefore yield rather than occupy its core; a non-cooperative wait
    /// makes waiters starve the workers doing the useful work, and a run that should take
    /// milliseconds can take orders of magnitude longer. This asserts only correctness -- the run
    /// completes and reaches the optimum inside a generous timeout -- deliberately not a
    /// wall-clock bound, which would be a flaky assertion on shared hardware.
    /// </summary>
    [Fact]
    public async Task OversubscribedWorkerCount_CompletesAndConverges()
    {
        var evaluator = new SphereEvaluator(dimension: 6);

        // Twice the core count guarantees oversubscription on any machine; the floor keeps the
        // test meaningful on a single-core agent.
        var workers = Math.Max(4, Environment.ProcessorCount * 2);

        var result = await RunAsync(evaluator, workers, OversubscribedTimeout);

        ConvergenceAssert.ReachedOptimum(evaluator, result, ValueTolerance);
    }

    private static async Task<DotNetDifferentialEvolution.Models.Population> RunAsync(
        SphereEvaluator evaluator,
        int workers,
        TimeSpan? timeout = null)
    {
        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(80)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.6, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1500))
            .UseProcessors(workers)
            .Build();

        var result = await de.RunAsync().WaitAsync(timeout ?? Timeout);
        result.MoveCursorToBestIndividual();
        return result;
    }
}
