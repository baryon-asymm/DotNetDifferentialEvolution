using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.Managers;

public class WorkersManager : IDisposable
{
    private Memory<WorkerController> _workerControllers;

    private IWorkerExecutor _workerExecutor;

    public WorkersManager(IWorkerExecutor executor)
    {
        _workerControllers = Memory<WorkerController>.Empty;
        
        _workerExecutor = executor;
        EnsureWorkerExecutor();
    }

    public void SetWorkerExecutor(IWorkerExecutor executor)
    {
        _workerExecutor = executor;
        EnsureWorkerExecutor();
    }

    private void EnsureWorkerExecutor()
    {
        if (_workerExecutor == null)
            throw new InvalidOperationException("The worker executor is not initialized.");
    }

    private void EnsureWorkerControllers()
    {
        if (_workerControllers.Length == 0)
            throw new InvalidOperationException("The worker controllers are not initialized.");
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}