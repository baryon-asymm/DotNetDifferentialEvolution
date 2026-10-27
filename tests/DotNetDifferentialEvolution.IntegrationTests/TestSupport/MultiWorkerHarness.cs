using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Wires up a master + N−1 slave <see cref="WorkerController"/>s around a single
/// <see cref="OrchestratorWorkerHandler"/>, mirroring how
/// <see cref="DifferentialEvolution"/> arranges its workers, so the
/// orchestration and concurrency tests can drive a genuine multi-threaded run and dispose every
/// worker cleanly.
/// </summary>
internal sealed class MultiWorkerHarness : IDisposable
{
    private readonly WorkerController[] _slaves;

    public MultiWorkerHarness(
        ProblemContext context,
        AlgorithmExecutor executor,
        int workersCount)
    {
        var slaveCount = workersCount - 1;
        _slaves = new WorkerController[slaveCount];
        for (var i = 0; i < slaveCount; i++)
        {
            _slaves[i] = new WorkerController(workerId: i, executor);
        }

        Handler = new OrchestratorWorkerHandler(_slaves.ToArray(), context);
        Master = new WorkerController(workerId: workersCount - 1, executor, Handler);
    }

    public OrchestratorWorkerHandler Handler { get; }

    /// <summary>Gets the worker that carries the handler. It completes the result task from
    /// inside its own loop and leaves the loop only after the handler returns.</summary>
    public WorkerController Master { get; }

    /// <summary>Starts every slave, then the master (the production order:
    /// <c>DifferentialEvolution.RunAsync</c> starts the workers in the order they were created,
    /// the master last).</summary>
    public void StartAll()
    {
        foreach (var slave in _slaves)
        {
            slave.Start();
        }

        Master.Start();
    }

    /// <summary>Gets whether any slave is still running. The handler stops every slave before
    /// the result task completes, so this is <see langword="false"/> as soon as a test resumes
    /// from awaiting the result. The master is not covered: it is still inside its loop at that
    /// moment (<see cref="WorkerStopAssert"/>).</summary>
    public bool AnySlaveRunning => _slaves.Any(slave => slave.IsRunning);

    public void Dispose()
    {
        Master.Dispose();
        foreach (var slave in _slaves)
        {
            slave.Dispose();
        }
    }
}
