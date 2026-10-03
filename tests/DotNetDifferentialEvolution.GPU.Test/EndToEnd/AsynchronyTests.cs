namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Checks 6a, 6b and 6c of the GPU package's ACCEPTANCE.md, asynchrony and cancellation, on the
/// CPU accelerator. Nothing is timed: the run is held on a gate the test opens
/// (<see cref="GateObserver"/>) and every wait is bounded only as a hang guard. The case
/// "a call during the run throws" is also B1's row "<c>RunAsync</c> while a run is in progress".
/// </summary>
public class AsynchronyTests
{
    private static readonly double[] Lower = [-5.0, -5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0, 5.0];

    /// <summary>
    /// 6a: while the observer is held at generation 1 on a gate the test holds, the task
    /// <c>RunAsync</c> returned is not complete. If the loop ran on the caller's thread,
    /// <c>RunAsync</c> would return only after the whole run, with a completed task.
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task RunAsyncReturnsAnIncompleteTaskWhileTheObserverIsHeld()
    {
        using var gate = new GateObserver(holdAtGeneration: 1, Environment.CurrentManagedThreadId);
        using var optimizer = Build(gate, maxGenerations: 5);

        var run = optimizer.RunAsync();
        bool entered;
        bool completedWhileHeld;
        try
        {
            entered = gate.WaitUntilEntered();
            completedWhileHeld = run.IsCompleted;
        }
        finally
        {
            gate.Release();
        }

        Assert.True(entered, "The observer was never called at generation 1.");
        Assert.False(gate.RanOnTheCallersThread, "The observer ran on the thread that called RunAsync.");
        Assert.False(completedWhileHeld, "RunAsync returned a completed task while the run was held at generation 1.");
        var result = await run.ConfigureAwait(true);
        Assert.False(gate.GateTimedOut);
        Assert.Equal(5, result.Generations);
    }

    /// <summary>
    /// 6b: a token cancelled from the observer at generation 3 ends the task as canceled, after at
    /// most 4 generations (counted by the observer, called every generation).
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ATokenCancelledAtGenerationThreeEndsTheTaskAsCanceled()
    {
        using var source = new CancellationTokenSource();
        var observer = new CancellingObserver(source, cancelAtGeneration: 3);
        using var optimizer = Build(observer, maxGenerations: 100);

        var run = optimizer.RunAsync(source.Token);

        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => run).ConfigureAwait(true);
        Assert.True(run.IsCanceled);
        Assert.InRange(observer.Calls, 3, 4);
    }

    /// <summary>6c: after the run, a second <c>RunAsync</c> returns the same task.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ASecondCallAfterTheRunReturnsTheSameTask()
    {
        using var optimizer = Build(new RecordingObserver(), maxGenerations: 5);

        var first = optimizer.RunAsync();
        _ = await first.ConfigureAwait(true);
        var second = optimizer.RunAsync();

        Assert.Same(first, second);
    }

    /// <summary>
    /// 6c, and B1's row "<c>RunAsync</c> while a run is in progress": while the run is held at
    /// generation 1, a second <c>RunAsync</c> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ACallDuringTheRunThrows()
    {
        using var gate = new GateObserver(holdAtGeneration: 1, Environment.CurrentManagedThreadId);
        using var optimizer = Build(gate, maxGenerations: 5);

        var run = optimizer.RunAsync();
        Exception? failure;
        try
        {
            Assert.True(gate.WaitUntilEntered(), "The observer was never called at generation 1.");
            failure = Record.Exception(() => { _ = optimizer.RunAsync(); });
        }
        finally
        {
            gate.Release();
        }

        _ = await run.ConfigureAwait(true);
        _ = Assert.IsType<InvalidOperationException>(failure);
    }

    private static GpuDifferentialEvolution Build(IGpuPopulationUpdatedHandler observer, int maxGenerations) =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(maxGenerations)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1)
            .WithPopulationUpdateHandler(observer)
            .Build();
}
