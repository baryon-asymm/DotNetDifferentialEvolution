using System.Diagnostics;
using DotNetDifferentialEvolution;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

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
    public async Task RepeatedBuildRunDispose_DoesNotLeakWorkerControllers()
    {
        var baseline = WorkerController.GlobalWorkerCounter;

        for (int i = 0; i < Iterations; i++)
        {
            using var de = BuildSmallOptimizer();
            await de.RunAsync().WaitAsync(Timeout);
        }

        // Every controller created across all iterations must have been disposed.
        Assert.Equal(baseline, WorkerController.GlobalWorkerCounter);
    }

    [Fact]
    public async Task RepeatedBuildRunDispose_DoesNotLeakThreads()
    {
        // Warm up so the thread pool / JIT threads are already created before we measure.
        using (var warmup = BuildSmallOptimizer())
            await warmup.RunAsync().WaitAsync(Timeout);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var baselineThreads = CurrentThreadCount();

        for (int i = 0; i < Iterations; i++)
        {
            using var de = BuildSmallOptimizer();
            await de.RunAsync().WaitAsync(Timeout);
        }

        // Give disposed worker threads a moment to fully exit, then confirm no unbounded growth.
        await Task.Delay(500);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var workers = Math.Max(2, Environment.ProcessorCount);
        var finalThreads = CurrentThreadCount();

        // A leak would add ~Iterations * workers threads; allow generous slack for the runtime.
        Assert.True(
            finalThreads <= baselineThreads + workers + 8,
            $"Thread count grew from {baselineThreads} to {finalThreads}; suspected worker-thread leak.");
    }

    private static DotNetDifferentialEvolution.DifferentialEvolution BuildSmallOptimizer() =>
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
