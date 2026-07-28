using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers;
using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution;

/// <summary>
/// Represents the Differential Evolution algorithm.
/// </summary>
public class DifferentialEvolution : IDisposable
{
    private bool _isDisposed;
    
    private readonly ProblemContext _problemContext;
    
    private readonly Memory<WorkerController> _workerControllers;
    private readonly OrchestratorWorkerHandler _orchestratorWorkerHandler;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DifferentialEvolution"/> class.
    /// </summary>
    /// <param name="problemContext">The context of the problem to solve.</param>
    /// <param name="algorithmExecutor">The executor for the algorithm.</param>
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

    /// <summary>
    /// Runs the Differential Evolution algorithm asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the final population.</returns>
    /// <remarks>
    /// Kept as a distinct overload rather than folded into the optional-parameter form below.
    /// Adding the parameter alone would have removed <c>RunAsync()</c> from the compiled surface —
    /// source-compatible, but any assembly built against 4.0.0 would fail with
    /// <see cref="MissingMethodException"/> at run time without a rebuild. Package validation
    /// against the published 4.0.0 package is what caught it.
    /// </remarks>
    public Task<Population> RunAsync() => RunAsync(CancellationToken.None);

    /// <summary>
    /// Runs the Differential Evolution algorithm asynchronously, observing a cancellation token.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that abandons the run. Cancellation is observed at the next generation barrier —
    /// the one point at which every worker has finished its stripe and none has started the next —
    /// so a run with an expensive objective stops within roughly one generation of the request,
    /// not instantly. The returned task then completes as canceled and every worker is stopped.
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the final population.</returns>
    /// <exception cref="OperationCanceledException">The run was canceled.</exception>
    public Task<Population> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var task = _orchestratorWorkerHandler.GetResultPopulationTask();

        if (task.IsCompleted)
            return task;

        if (cancellationToken.IsCancellationRequested)
        {
            _orchestratorWorkerHandler.CancelBeforeStart(cancellationToken);

            return _orchestratorWorkerHandler.GetResultPopulationTask();
        }

        _orchestratorWorkerHandler.UseCancellationToken(cancellationToken);

        foreach (var workerController in _workerControllers.Span)
            workerController.Start(throwIfRunning: true);

        return _orchestratorWorkerHandler.GetResultPopulationTask();
    }
    
    /// <summary>
    /// Releases the resources used by the <see cref="DifferentialEvolution"/> class.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="DifferentialEvolution"/> class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">A value indicating whether to release both managed and unmanaged resources.</param>
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
    
    /// <summary>
    /// Finalizes an instance of the <see cref="DifferentialEvolution"/> class.
    /// </summary>
    ~DifferentialEvolution()
    {
        Dispose(disposing: false);
    }
}
