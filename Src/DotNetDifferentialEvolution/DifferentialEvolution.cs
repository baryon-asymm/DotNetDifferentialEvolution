using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution;

public class DifferentialEvolution : IDisposable
{
    private bool _isDisposed;
    
    private readonly ProblemContext _problemContext;
    
    private readonly Memory<WorkerController> _workerControllers;
    private readonly OrchestratorWorkerHandler _orchestratorWorkerHandler;
    
    public DifferentialEvolution(
        ProblemContext problemContext,
        IAlgorithmExecutor algorithmExecutor)
    {
        ArgumentNullException.ThrowIfNull(problemContext);
        ArgumentNullException.ThrowIfNull(algorithmExecutor);
        
        _problemContext = problemContext;

        var workers = new List<WorkerController>(_problemContext.WorkersCount);
        var slaveWorkersCount = _problemContext.WorkersCount - 1;
        for (int i = 0; i < slaveWorkersCount; i++)
            workers.Add(new WorkerController(workerId: i, algorithmExecutor));

        _orchestratorWorkerHandler = new OrchestratorWorkerHandler(workers.ToArray(), _problemContext);
        
        var masterWorkerId = _problemContext.WorkersCount - 1;
        var masterWorker = new WorkerController(masterWorkerId, algorithmExecutor, _orchestratorWorkerHandler);
        workers.Add(masterWorker);
        
        _workerControllers = workers.ToArray();
    }

    public Task<Population> GetResultAsync()
    {
        var task = _orchestratorWorkerHandler.GetResultPopulationTask();
        
        if (task.IsCompleted)
            return task;
        
        foreach (var workerController in _workerControllers.Span)
            workerController.Start(throwIfRunning: true);
        
        return _orchestratorWorkerHandler.GetResultPopulationTask();
    }
    
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            foreach (var workerController in _workerControllers.Span)
                workerController.Dispose();
        }
        
        _isDisposed = true;
    }
    
    ~DifferentialEvolution()
    {
        Dispose(disposing: false);
    }
}
