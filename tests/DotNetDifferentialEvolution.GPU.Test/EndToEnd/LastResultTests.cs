namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check A16 of the GPU package's Kernels ACCEPTANCE.md: a cancelled run keeps its best individual in
/// <c>LastResult</c>. (a) <c>null</c> before the run and while an observer holds it; (b) a token cancelled by an observer due
/// every 10 generations, at generation 100: the task is canceled and <c>LastResult</c> is the best individual of that
/// snapshot, with its counts, bit for bit; (c) a <c>Dispose</c> while an observer holds the run at generation 3: the same
/// for generation 3, and A7's buffers disposed after; (d) a completed run: the awaited result, the same object; (e) an
/// observer that throws: faulted, <c>LastResult</c> <c>null</c>. (b) also runs on CUDA, under <c>Gpu</c>.
/// </summary>
public class LastResultTests
{
    private const int CancelledAt = 100;
    private const int HeldAt = 3;

    private static readonly double[] Lower = [-5.0, -5.0, -5.0];
    private static readonly double[] Upper = [5.0, 5.0, 5.0];

    /// <summary>A16 (a): <c>LastResult</c> is <c>null</c> before <c>RunAsync</c> and while an observer holds the run.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task LastResultIsNullBeforeTheRunAndWhileTheObserverHoldsIt()
    {
        using var gate = new GateObserver(HeldAt, Environment.CurrentManagedThreadId);
        using var guard = new BoundedDisposal(Build(GpuDevice.Cpu, 16, gate, everyNGenerations: 1, generations: 20));
        var optimizer = guard.Optimizer;
        Assert.Null(optimizer.LastResult);

        var run = optimizer.RunAsync();
        HeldRun held;
        try
        {
            Assert.True(gate.WaitUntilEntered(), "The observer was never called at generation 3.");
            held = new HeldRun(optimizer.LastResult, run.IsCompleted);
        }
        finally
        {
            gate.Release();
        }

        _ = await run.WaitAsync(HangGuard.Limit).ConfigureAwait(true);
        Assert.Null(held.Seen);
        Assert.False(held.RunCompleted);
    }

    /// <summary>A16 (b), on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task ACancelledTokenLeavesTheBestIndividualOfTheSnapshotOnTheCpuAccelerator() =>
        TheTokenIsCancelledAtGenerationOneHundred(GpuDevice.Cpu, 16);

    /// <summary>A16 (b), on CUDA, the orchestrator's to run: N = 64.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public Task ACancelledTokenLeavesTheBestIndividualOfTheSnapshotOnCuda() =>
        TheTokenIsCancelledAtGenerationOneHundred(GpuDevice.Cuda, 64);

    /// <summary>
    /// A16 (c): a <c>Dispose</c> while the observer holds the run at generation 3 ends it canceled, with the best individual of
    /// generation 3 in <c>LastResult</c>, and every buffer and kernel the optimizer allocated is released after.
    /// </summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task DisposeWhileTheObserverHoldsTheRunLeavesTheBestIndividualOfThatGeneration()
    {
        using var gate = new GateObserver(HeldAt, Environment.CurrentManagedThreadId);
        var keeper = new KeepingObserver(HeldAt, gate);
        using var guard = new BoundedDisposal(Build(GpuDevice.Cpu, 16, keeper, everyNGenerations: 1, generations: 1000));
        var optimizer = guard.Optimizer;
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
        Assert.False(gate.GateTimedOut);
        AssertIsTheKeptBest(optimizer.LastResult, keeper.Kept, HeldAt);
        Assert.All(allocated, item => Assert.True(item.IsDisposed, $"A {item.GetType().Name} was not released."));
    }

    /// <summary>A16 (d): after a completed run, <c>LastResult</c> is the awaited result, the same object.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task ACompletedRunLeavesTheResultTheTaskReturned()
    {
        using var optimizer = Build(GpuDevice.Cpu, 16, observer: null, everyNGenerations: 1, generations: 50);
        Assert.Null(optimizer.LastResult);

        var result = await optimizer.RunAsync().WaitAsync(HangGuard.Limit).ConfigureAwait(true);

        Assert.Equal(50, result.Generations);
        Assert.Same(result, optimizer.LastResult);
    }

    /// <summary>A16 (e): a run whose observer throws is faulted and leaves <c>LastResult</c> <c>null</c>.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public async Task AFaultedRunLeavesNothing()
    {
        var thrown = new InvalidOperationException("The observer failed at generation 2.");
        using var optimizer = Build(
            GpuDevice.Cpu, 16, new ThrowingObserver(thrown, throwAtGeneration: 2), everyNGenerations: 1, generations: 50);

        var run = optimizer.RunAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);

        Assert.Same(thrown, failure);
        Assert.True(run.IsFaulted);
        Assert.Null(optimizer.LastResult);
    }

    private static async Task TheTokenIsCancelledAtGenerationOneHundred(GpuDevice device, int populationSize)
    {
        using var source = new CancellationTokenSource();
        var keeper = new KeepingObserver(CancelledAt, new CancellingObserver(source, CancelledAt));
        using var optimizer = Build(device, populationSize, keeper, everyNGenerations: 10, generations: 1000);
        Assert.Null(optimizer.LastResult);

        var run = optimizer.RunAsync(source.Token);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(HangGuard.Limit)).ConfigureAwait(true);
        Assert.True(run.IsCanceled);
        AssertIsTheKeptBest(optimizer.LastResult, keeper.Kept, CancelledAt);
    }

    private static void AssertIsTheKeptBest(GpuOptimizationResult? result, KeptBest? kept, int generation)
    {
        var expected = Assert.IsType<KeptBest>(kept);
        var actual = Assert.IsType<GpuOptimizationResult>(result);
        Assert.Equal(generation, expected.Generation);
        Assert.Equal(generation, actual.Generations);
        Assert.Equal(expected.EvaluationCount, actual.EvaluationCount);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Fitness), BitConverter.DoubleToInt64Bits(actual.FitnessFunctionValue));
        Assert.Equal(Bits(expected.Genes), Bits(actual.Genes.Span));
    }

    private static long[] Bits(ReadOnlySpan<double> values)
    {
        var bits = new long[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            bits[i] = BitConverter.DoubleToInt64Bits(values[i]);
        }

        return bits;
    }

    private static GpuDifferentialEvolution Build(
        GpuDevice device, int populationSize, IGpuPopulationUpdatedHandler? observer, int everyNGenerations, int generations)
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(populationSize)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(generations)
            .OnDevice(device)
            .WithSeed(1);
        return (observer is null ? stage : stage.WithPopulationUpdateHandler(observer, everyNGenerations)).Build();
    }

    /// <summary>What <c>LastResult</c> and the task showed while the observer held the run.</summary>
    /// <param name="Seen">What <c>LastResult</c> returned.</param>
    /// <param name="RunCompleted">Whether the task was complete.</param>
    private sealed record HeldRun(GpuOptimizationResult? Seen, bool RunCompleted);
}
