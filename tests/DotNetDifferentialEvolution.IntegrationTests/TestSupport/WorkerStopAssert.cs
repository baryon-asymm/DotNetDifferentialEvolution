using System.Diagnostics;
using DotNetDifferentialEvolution.Controllers;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Asserts that a worker leaves its loop by itself. The result task completes from inside the
/// loop of the worker that carries the handler, and that worker leaves the loop only after the
/// handler returns, so its <see cref="WorkerController.IsRunning"/> may still be
/// <see langword="true"/> when a test resumes from awaiting the result. The package does not
/// promise otherwise; this is what it does promise, checked.
/// </summary>
internal static class WorkerStopAssert
{
    /// <summary>The bound on the wait. It catches a worker that never leaves its loop; it is not
    /// a measurement, since the worker has only to return from the handler.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Waits until <paramref name="worker"/> reads as not running and fails if it still runs at the
    /// <see cref="Deadline"/>. Never stops or disposes the worker: it has to leave on its own.
    /// </summary>
    /// <param name="worker">The worker that carries the handler.</param>
    /// <returns>A task that completes once the worker has left its loop.</returns>
    public static async Task LeavesItsLoopAsync(WorkerController worker)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var stopwatch = Stopwatch.StartNew();
        while (worker.IsRunning && stopwatch.Elapsed < Deadline)
        {
            await Task.Delay(1).ConfigureAwait(true);
        }

        Assert.False(
            worker.IsRunning,
            $"Worker {worker.WorkerId} is still inside its loop {Deadline.TotalSeconds:0} s after the result.");
    }
}
