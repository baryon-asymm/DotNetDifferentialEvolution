using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A8 of the GPU package's Kernels ACCEPTANCE.md, on the CPU accelerator: an exception of any type thrown on the run's
/// thread, an <see cref="OutOfMemoryException"/> from the observer included, faults the task with it and the process lives;
/// and two threads calling <c>Dispose</c> during a run, neither returns before the run has stopped and everything
/// <see cref="DisposeTests"/> reads is disposed.
/// </summary>
public class RunThreadTests
{
    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>A8: whatever type the observer throws, the task faults with that same exception.</summary>
    /// <param name="type">The exception's type.</param>
    /// <returns>The case.</returns>
    [Theory]
    [InlineData(typeof(OutOfMemoryException))]
    [InlineData(typeof(AccessViolationException))]
    [InlineData(typeof(InsufficientMemoryException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(AggregateException))]
    public async Task AnyExceptionOnTheRunThreadFaultsTheTaskWithIt(Type type)
    {
        var thrown = (Exception)Activator.CreateInstance(type)!;
        using var guard = new BoundedDisposal(Stage(new ThrowingObserver(thrown, throwAtGeneration: 2), generations: 10).Build());
        var optimizer = guard.Optimizer;

        var run = optimizer.RunAsync();
        var failure = await Assert.ThrowsAsync(type, () => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);

        Assert.Same(thrown, failure);
        Assert.True(run.IsFaulted);
    }

    /// <summary>
    /// A8: a second <c>Dispose</c> from another thread, while the first waits for a run held in the observer, does not return
    /// until the run has stopped and everything is disposed.
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ASecondDisposeWaitsUntilTheFirstHasStoppedTheRunAndFreedEverything()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var gate = new GateObserver(holdAtGeneration: 3, Environment.CurrentManagedThreadId);
        using var guard = new BoundedDisposal(Stage(gate, generations: 1000, accelerator).Build());
        var optimizer = guard.Optimizer;
        var allocated = optimizer.Allocated.ToList();
        Assert.NotEmpty(allocated);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        optimizer.SecondDisposeWaiting = () => waiting.TrySetResult();

        var run = optimizer.RunAsync();
        Task first;
        Task<(bool Stopped, bool Freed)> second;
        Task winner;
        try
        {
            Assert.True(gate.WaitUntilEntered(), "The observer was never called at generation 3.");
            first = Task.Run(optimizer.Dispose);
            Assert.True(SpinWait.SpinUntil(() => optimizer.DisposeRequested, HangGuard.Limit), "Dispose never asked the run to stop.");
            second = Task.Run(() => DisposeAndLook(optimizer, run, allocated));
            winner = await Task.WhenAny(waiting.Task, second).WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        }
        finally
        {
            gate.Release();
        }

        Assert.Same(waiting.Task, winner);
        await Task.WhenAll(first, second).WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        Assert.Equal((true, true), await second.ConfigureAwait(true));
        Assert.True(run.IsCanceled);
    }

    /// <summary>
    /// A8: a <c>Dispose</c> from the observer, which the run's thread finishes, is waited for by a second <c>Dispose</c> from
    /// another thread: the second returns only once everything is disposed.
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ASecondDisposeWaitsForTheRunThreadsReleaseAfterAnObserversDispose()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var hold = new ManualResetEventSlim();
        var observer = new DisposingObserver(disposeAtGeneration: 3, holdUntil: hold);
        using var guard = new BoundedDisposal(Stage(observer, generations: 1000, accelerator).Build());
        var optimizer = guard.Optimizer;
        observer.Optimizer = optimizer;
        var allocated = optimizer.Allocated.ToList();
        Assert.NotEmpty(allocated);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        optimizer.SecondDisposeWaiting = () => waiting.TrySetResult();

        var run = optimizer.RunAsync();
        Task<(bool Stopped, bool Freed)> second;
        Task winner;
        try
        {
            Assert.True(SpinWait.SpinUntil(() => optimizer.DisposeRequested, HangGuard.Limit), "The observer never disposed.");
            second = Task.Run(() => DisposeAndLook(optimizer, run, allocated));
            winner = await Task.WhenAny(waiting.Task, second).WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        }
        finally
        {
            hold.Set();
        }

        Assert.Same(waiting.Task, winner);
        var (_, freed) = await second.WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        Assert.True(freed, "The second Dispose returned before everything was disposed.");
        Assert.False(observer.HoldTimedOut);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);
    }

    /// <summary>Calls <c>Dispose</c>, and says on return whether the run had ended and everything was disposed.</summary>
    private static (bool Stopped, bool Freed) DisposeAndLook(
        GpuDifferentialEvolution optimizer, Task run, IReadOnlyList<ILGPU.Util.DisposeBase> allocated)
    {
        optimizer.Dispose();
        return (run.IsCompleted, allocated.All(item => item.IsDisposed));
    }

    private static GpuBuilder<Sphere> Stage(IGpuPopulationUpdatedHandler observer, int generations, Accelerator? accelerator = null)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithJade()
            .WithGenerationLimit(generations);
        var device = accelerator is null ? stage.OnDevice(GpuDevice.Cpu) : stage.OnAccelerator(accelerator);
        return (GpuBuilder<Sphere>)device.WithSeed(1).WithPopulationUpdateHandler(observer);
    }
}
