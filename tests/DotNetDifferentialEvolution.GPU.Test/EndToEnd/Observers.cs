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
