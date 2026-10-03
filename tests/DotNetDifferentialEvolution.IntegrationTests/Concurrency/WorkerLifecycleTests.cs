using System.Diagnostics;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.IntegrationTests.Concurrency;

/// <summary>
/// Verifies that worker threads are shut down and released cleanly: repeatedly building,
/// running, and disposing optimizers must not leak <see cref="WorkerController"/>s or OS
/// threads. <see cref="WorkerController.GlobalWorkerCounter"/> is the authoritative, leak-free
/// signal (incremented on construction, decremented on disposal); the OS thread count is a
/// coarser secondary guard.
/// </summary>
[Trait("Category", "Integration")]
public class WorkerLifecycleTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private const int Iterations = 25;

    [Fact]
    public async Task RepeatedBuildRunDisposeDoesNotLeakWorkerControllers()
    {
        var baseline = WorkerController.GlobalWorkerCounter;

        for (var i = 0; i < Iterations; i++)
        {
            using var de = BuildSmallOptimizer();
            _ = await de.RunAsync().WaitAsync(Timeout).ConfigureAwait(true);
        }

        // Every controller created across all iterations must have been disposed.
        Assert.Equal(baseline, WorkerController.GlobalWorkerCounter);
    }

    [Fact]
    public async Task RepeatedBuildRunDisposeDoesNotLeakThreads()
    {
        // Warm up so the thread pool / JIT threads are already created before we measure.
        using (var warmup = BuildSmallOptimizer())
        {
            _ = await warmup.RunAsync().WaitAsync(Timeout).ConfigureAwait(true);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var baselineThreads = CurrentThreadCount();

        for (var i = 0; i < Iterations; i++)
        {
            using var de = BuildSmallOptimizer();
            _ = await de.RunAsync().WaitAsync(Timeout).ConfigureAwait(true);
        }

        // Give disposed worker threads a moment to fully exit, then confirm no unbounded growth.
        await Task.Delay(500).ConfigureAwait(true);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var workers = Math.Max(2, Environment.ProcessorCount);
        var finalThreads = CurrentThreadCount();

        // A leak would add ~Iterations * workers threads; allow generous slack for the runtime.
        Assert.True(
            finalThreads <= baselineThreads + workers + 8,
            $"Thread count grew from {baselineThreads} to {finalThreads}; suspected worker-thread leak.");
    }

    private static DifferentialEvolution BuildSmallOptimizer() =>
        DifferentialEvolutionBuilder.ForFunction(new SphereEvaluator(dimension: 4))
            .WithBounds(new SphereEvaluator(4).GetLowerBounds(), new SphereEvaluator(4).GetUpperBounds())
            .WithPopulationSize(40)
            .WithUniformPopulationSampling()
            .WithDefaultMutationStrategy(0.6, 0.9)
            .WithDefaultSelectionStrategy()
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(300))
            .UseProcessors(Math.Max(2, Environment.ProcessorCount))
            .Build();

    private static int CurrentThreadCount()
    {
        using var process = Process.GetCurrentProcess();
        return process.Threads.Count;
    }
}
