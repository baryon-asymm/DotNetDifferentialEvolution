using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.Concurrency;

/// <summary>
/// A smoke check that repeated optimizer lifecycles do not grow the managed heap without
/// bound. This is intentionally coarse (GC timing is non-deterministic); precise allocation
/// and throughput profiling lives in the Benchmark project, not in assertions. Tagged Slow.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Slow")]
public class ResourceUsageTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task RepeatedRunsDoNotGrowManagedHeapUnbounded()
    {
        // Settle and establish a baseline after a warmup run.
        using (var warmup = BuildOptimizer())
            await warmup.RunAsync().WaitAsync(Timeout);

        var baseline = GetSettledManagedMemory();

        for (int i = 0; i < 50; i++)
        {
            using var de = BuildOptimizer();
            await de.RunAsync().WaitAsync(Timeout);
        }

        var after = GetSettledManagedMemory();

        // A 32 MB ceiling on growth comfortably absorbs GC noise while still catching a real,
        // accumulating leak across 50 build/run/dispose cycles.
        const long ceilingBytes = 32L * 1024 * 1024;
        Assert.True(
            after - baseline <= ceilingBytes,
            $"Managed heap grew by {(after - baseline) / (1024.0 * 1024.0):F1} MB across 50 cycles.");
    }

    private static long GetSettledManagedMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static DotNetDifferentialEvolution.DifferentialEvolution BuildOptimizer() =>
        DifferentialEvolutionBuilder.ForFunction(new SphereEvaluator(dimension: 5))
            .WithBounds(new SphereEvaluator(5).GetLowerBounds(), new SphereEvaluator(5).GetUpperBounds())
            .WithPopulationSize(60)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.6, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(500))
            .UseProcessors(Math.Max(2, Environment.ProcessorCount))
            .Build();
}
