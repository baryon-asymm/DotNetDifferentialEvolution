using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers;

namespace DotNetDifferentialEvolution.Managers;

public class WorkersManager : IDisposable
{
    private readonly Memory<WorkerController> _workerControllers;

    private IAlgorithmExecutor _algorithmExecutor;

    public WorkersManager(
        IAlgorithmExecutor executor)
    {
        _workerControllers = Memory<WorkerController>.Empty;

        _algorithmExecutor = executor;
        EnsureWorkerExecutor();
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }

    public void SetWorkerExecutor(
        IAlgorithmExecutor executor)
    {
        _algorithmExecutor = executor;
        EnsureWorkerExecutor();
    }

    private void EnsureWorkerExecutor()
    {
        if (_algorithmExecutor == null)
            throw new InvalidOperationException("The worker executor is not initialized.");
    }

    private void EnsureWorkerControllers()
    {
        if (_workerControllers.Length == 0)
            throw new InvalidOperationException("The worker controllers are not initialized.");
    }
}
