using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Wires up a master + N−1 slave <see cref="WorkerController"/>s around a single
/// <see cref="OrchestratorWorkerHandler"/>, mirroring how
/// <see cref="DotNetDifferentialEvolution.DifferentialEvolution"/> arranges its workers, so the
/// orchestration and concurrency tests can drive a genuine multi-threaded run and dispose every
/// worker cleanly.
/// </summary>
internal sealed class MultiWorkerHarness : IDisposable
{
    private readonly WorkerController _master;
    private readonly WorkerController[] _slaves;

    public MultiWorkerHarness(
        ProblemContext context,
        AlgorithmExecutor executor,
        int workersCount)
    {
        var slaveCount = workersCount - 1;
        _slaves = new WorkerController[slaveCount];
        for (int i = 0; i < slaveCount; i++)
            _slaves[i] = new WorkerController(workerId: i, executor);

        Handler = new OrchestratorWorkerHandler(_slaves.ToArray(), context);
        _master = new WorkerController(workerId: workersCount - 1, executor, Handler);
    }

    public OrchestratorWorkerHandler Handler { get; }

    /// <summary>Starts the master first, then every slave (matching the production order).</summary>
    public void StartAll()
    {
        _master.Start();
        foreach (var slave in _slaves)
            slave.Start();
    }

    /// <summary>Gets whether any worker (master or slave) is still running.</summary>
    public bool AnyRunning => _master.IsRunning || _slaves.Any(slave => slave.IsRunning);

    public void Dispose()
    {
        _master.Dispose();
        foreach (var slave in _slaves)
            slave.Dispose();
    }
}
