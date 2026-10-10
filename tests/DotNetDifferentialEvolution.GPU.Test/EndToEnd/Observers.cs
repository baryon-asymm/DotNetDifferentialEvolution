namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>Bounds every wait of the run cases, so a hang fails the case instead of stalling the suite. Nothing is timed.</summary>
internal static class HangGuard
{
    /// <summary>The longest any case waits for a signal that, in a correct run, always comes.</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(2);
}

/// <summary>Counts its calls and keeps the generations and the last snapshot it saw.</summary>
internal sealed class RecordingObserver : IGpuPopulationUpdatedHandler
{
    private readonly List<int> _generations = [];

    /// <summary>Gets the generation of each call, in order.</summary>
    public IReadOnlyList<int> Generations => _generations;

    /// <summary>Gets the last snapshot, or <see langword="null"/> before the first call.</summary>
    public GpuPopulationSnapshot? Last { get; private set; }

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        _generations.Add(snapshot.Generation);
        Last = snapshot;
    }
}

/// <summary>
/// Holds the run at one generation on a gate the test opens. It signals entry (<see cref="WaitUntilEntered"/>)
/// first, then waits for <see cref="Release"/>, unless it is called on the thread that started the
/// run: then nothing could ever open the gate, so it records that and returns at once instead of
/// deadlocking the case.
/// </summary>
/// <param name="holdAtGeneration">The generation to hold at.</param>
/// <param name="callerThreadId">The managed thread id of the thread that calls <c>RunAsync</c>.</param>
internal sealed class GateObserver(int holdAtGeneration, int callerThreadId) : IGpuPopulationUpdatedHandler, IDisposable
{
    private readonly ManualResetEventSlim _entered = new();
    private readonly ManualResetEventSlim _gate = new();

    /// <summary>Gets a value indicating whether the observer ran on the thread that called <c>RunAsync</c>.</summary>
    public bool RanOnTheCallersThread { get; private set; }

    /// <summary>Gets a value indicating whether the gate's hang guard expired before the test opened it.</summary>
    public bool GateTimedOut { get; private set; }

    /// <summary>Waits until the observer has been called at the hold generation.</summary>
    /// <returns><see langword="true"/> when it was, within the hang guard.</returns>
    public bool WaitUntilEntered() => _entered.Wait(HangGuard.Limit);

    /// <summary>Opens the gate.</summary>
    public void Release() => _gate.Set();

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        if (snapshot.Generation != holdAtGeneration)
        {
            return;
        }

        _entered.Set();
        if (Environment.CurrentManagedThreadId == callerThreadId)
        {
            RanOnTheCallersThread = true;
            return;
        }

        GateTimedOut = !_gate.Wait(HangGuard.Limit);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _entered.Dispose();
        _gate.Dispose();
    }
}

/// <summary>Cancels a token source at one generation, and counts every call.</summary>
/// <param name="source">The source to cancel.</param>
/// <param name="cancelAtGeneration">The generation to cancel at.</param>
internal sealed class CancellingObserver(CancellationTokenSource source, int cancelAtGeneration) : IGpuPopulationUpdatedHandler
{
    /// <summary>Gets the number of calls, which is the number of generations run.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        Calls++;
        if (snapshot.Generation == cancelAtGeneration)
        {
            source.Cancel();
        }
    }
}

/// <summary>Throws a given exception at one generation.</summary>
/// <param name="failure">The exception to throw.</param>
/// <param name="throwAtGeneration">The generation to throw at.</param>
internal sealed class ThrowingObserver(Exception failure, int throwAtGeneration) : IGpuPopulationUpdatedHandler
{
    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        if (snapshot.Generation == throwAtGeneration)
        {
            throw failure;
        }
    }
}

/// <summary>
/// Disposes the optimizer it is given at one generation, from the run's thread, and keeps what the call threw. When
/// <paramref name="holdUntil"/> is given, it then waits for it, so that the run stays inside the observer after the call.
/// </summary>
/// <param name="disposeAtGeneration">The generation to dispose at.</param>
/// <param name="holdUntil">Opened by the test to let the observer return, or <see langword="null"/> to return at once.</param>
internal sealed class DisposingObserver(int disposeAtGeneration, ManualResetEventSlim? holdUntil = null) : IGpuPopulationUpdatedHandler
{
    /// <summary>Gets or sets the optimizer to dispose.</summary>
    public GpuDifferentialEvolution? Optimizer { get; set; }

    /// <summary>Gets what <c>Dispose</c> threw on the run's thread, or <see langword="null"/>.</summary>
    public Exception? DisposeFailure { get; private set; }

    /// <summary>Gets a value indicating whether the hold's hang guard expired before the test opened it.</summary>
    public bool HoldTimedOut { get; private set; }

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Generation != disposeAtGeneration)
        {
            return;
        }

        DisposeFailure = Record.Exception(Optimizer!.Dispose);
        if (holdUntil is not null)
        {
            HoldTimedOut = !holdUntil.Wait(HangGuard.Limit);
        }
    }
}

/// <summary>
/// Owns an optimizer a case holds in a run, and disposes it at the end of the case within <see cref="HangGuard.Limit"/>: a
/// <c>Dispose</c> that waits for a release which never comes then fails the case instead of hanging the test host.
/// </summary>
/// <param name="optimizer">The optimizer.</param>
internal sealed class BoundedDisposal(GpuDifferentialEvolution optimizer) : IDisposable
{
    /// <summary>Gets the optimizer.</summary>
    public GpuDifferentialEvolution Optimizer { get; } = optimizer;

    /// <inheritdoc />
    public void Dispose()
    {
        var disposing = Task.Run(Optimizer.Dispose);
        if (!disposing.Wait(HangGuard.Limit))
        {
            throw new TimeoutException("Dispose did not return within the hang guard: it waits for a release that never comes.");
        }
    }
}

/// <summary>The best individual of one snapshot, by the result's rule, kept by <see cref="KeepingObserver"/>.</summary>
/// <param name="Generation">The snapshot's generation.</param>
/// <param name="EvaluationCount">The snapshot's evaluations.</param>
/// <param name="Genes">The genes of the best individual.</param>
/// <param name="Fitness">The fitness of the best individual.</param>
internal sealed record KeptBest(int Generation, long EvaluationCount, double[] Genes, double Fitness)
{
    /// <summary>
    /// Picks the best individual of a snapshot as the package's result does: the lowest fitness, a <c>NaN</c> the worst, a
    /// tie to the lowest index. Written here from that rule, not taken from the package.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The best individual with the snapshot's counts.</returns>
    public static KeptBest Of(GpuPopulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var fitness = snapshot.FitnessFunctionValues.Span;
        var best = 0;
        for (var i = 1; i < fitness.Length; i++)
        {
            if ((double.IsNaN(fitness[best]) && !double.IsNaN(fitness[i])) || fitness[i] < fitness[best])
            {
                best = i;
            }
        }

        var genomeSize = snapshot.GenomeSize;
        return new KeptBest(
            snapshot.Generation,
            snapshot.EvaluationCount,
            snapshot.Genes.Span.Slice(best * genomeSize, genomeSize).ToArray(),
            fitness[best]);
    }
}

/// <summary>
/// Keeps the best individual of the snapshot at one generation (<see cref="KeptBest"/>), then hands every call, that one
/// included, to the observer it wraps, if any.
/// </summary>
/// <param name="keepAtGeneration">The generation to keep.</param>
/// <param name="inner">The observer to call after, or <see langword="null"/>.</param>
internal sealed class KeepingObserver(int keepAtGeneration, IGpuPopulationUpdatedHandler? inner = null) : IGpuPopulationUpdatedHandler
{
    /// <summary>Gets what was kept, or <see langword="null"/> before the generation was reached.</summary>
    public KeptBest? Kept { get; private set; }

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Generation == keepAtGeneration)
        {
            Kept = KeptBest.Of(snapshot);
        }

        inner?.Handle(snapshot);
    }
}
