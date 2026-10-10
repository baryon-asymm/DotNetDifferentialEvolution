using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A7 of the GPU package's Kernels ACCEPTANCE.md: <c>Dispose</c> frees what the optimizer allocated and stops its run.
/// On the CPU accelerator, a caller's, every buffer and kernel the optimizer allocated is read through the internal
/// <c>Allocated</c> (a list taken before the call) and must be <c>IsDisposed</c> afterwards, and the caller's accelerator must
/// still allocate and run: (a) after a <c>Dispose</c> while an observer holds the run at generation 3, which ends the task
/// canceled; (b) after a <c>Dispose</c> from the observer, once the task has ended; (c) after a normal run and <c>Dispose</c>.
/// </summary>
public class DisposeTests
{
    private const int HeldAt = 3;

    private static readonly double[] Lower = [-5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0];

    /// <summary>A7 (a): a <c>Dispose</c> while the observer holds the run stops it, canceled, and frees everything.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task DisposeWhileTheObserverHoldsTheRunCancelsItAndFreesEverything()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var gate = new GateObserver(HeldAt, Environment.CurrentManagedThreadId);
        using var optimizer = Build(accelerator, gate, generations: 1000);
        var allocated = optimizer.Allocated.ToList();
        Assert.NotEmpty(allocated);

        var run = optimizer.RunAsync();
        Task disposing;
        try
        {
            Assert.True(gate.WaitUntilEntered(), "The observer was never called at generation 3.");
            disposing = Task.Run(optimizer.Dispose);
            Assert.True(SpinWait.SpinUntil(() => optimizer.DisposeRequested, HangGuard.Limit), "Dispose never asked the run to stop.");
        }
        finally
        {
            gate.Release();
        }

        await disposing.WaitAsync(HangGuard.Limit).ConfigureAwait(true);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);
        Assert.True(run.IsCanceled);
        AssertAllDisposed(allocated);
        AssertTheAcceleratorIsUsable(accelerator);
    }

    /// <summary>A7 (b): a <c>Dispose</c> from the observer ends the run, and by the time the task has ended everything is freed.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task DisposeFromTheObserverFreesEverythingByTheTimeTheTaskEnds()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        var observer = new DisposingObserver(HeldAt);
        using var optimizer = Build(accelerator, observer, generations: 1000);
        observer.Optimizer = optimizer;
        var allocated = optimizer.Allocated.ToList();
        Assert.NotEmpty(allocated);

        var run = optimizer.RunAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);

        Assert.True(run.IsCanceled);
        Assert.Null(observer.DisposeFailure);
        AssertAllDisposed(allocated);
        AssertTheAcceleratorIsUsable(accelerator);
    }

    /// <summary>A7 (c): after a normal run, <c>Dispose</c> frees everything.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task DisposeAfterANormalRunFreesEverything()
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var optimizer = Build(accelerator, observer: null, generations: 20);
        var allocated = optimizer.Allocated.ToList();
        Assert.NotEmpty(allocated);

        var result = await optimizer.RunAsync().WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        Assert.Equal(20, result.Generations);
        optimizer.Dispose();

        AssertAllDisposed(allocated);
        AssertTheAcceleratorIsUsable(accelerator);
    }

    private static GpuDifferentialEvolution Build(Accelerator accelerator, IGpuPopulationUpdatedHandler? observer, int generations)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(16)
            .WithJade()
            .WithGenerationLimit(generations)
            .OnAccelerator(accelerator)
            .WithSeed(1);
        return (observer is null ? stage : stage.WithPopulationUpdateHandler(observer)).Build();
    }

    private static void AssertAllDisposed(IReadOnlyList<ILGPU.Util.DisposeBase> allocated) =>
        Assert.All(allocated, item => Assert.True(item.IsDisposed, $"A {item.GetType().Name} was not released."));

    /// <summary>The caller's accelerator still allocates, runs a kernel into the buffer and reads it back (check 7b's test).</summary>
    private static void AssertTheAcceleratorIsUsable(Accelerator accelerator)
    {
        const int length = 16;
        using var buffer = accelerator.Allocate1D<double>(length);
        var fill = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<double>>(OwnershipTests.FillWithIndex);
        fill(length, buffer.View);
        accelerator.Synchronize();

        Assert.Equal(Enumerable.Range(0, length).Select(k => (double)k), buffer.GetAsArray1D());
    }
}
