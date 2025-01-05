using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;

namespace DotNetDifferentialEvolution.Controllers;

/// <summary>
/// Manages the execution of a worker thread for the differential evolution algorithm.
/// </summary>
public class WorkerController : IDisposable
{
    private static volatile int _globalWorkerCounter = 0;
    
    private readonly object _lock = new();
    
    private bool _isDisposed;
    
    private readonly int _workerId;
    private readonly string _workerThreadName;

    private volatile bool _isPassLoopCompleted;
    private volatile bool _passLoopPermitted;

    private volatile bool _isPreparingToRun;
    private volatile bool _isRunning;
    private volatile bool _workerShouldStop;
    
    private volatile int _bestHandledIndividualIndex;
    
    private Thread? _workerThread;
    private Exception? _exception;

    private readonly IAlgorithmExecutor _algorithmExecutor;
    
    private readonly IWorkerPassLoopDoneHandler? _workerPassLoopDoneHandler;

    /// <summary>
    /// Gets a value indicating whether the worker is running.
    /// </summary>
    public bool IsRunning => _isRunning;
    
    /// <summary>
    /// Gets a value indicating whether the worker has encountered an exception.
    /// </summary>
    public bool HasException => _exception != null;

    /// <summary>
    /// Gets the exception encountered by the worker, if any.
    /// </summary>
    public Exception? Exception => _exception;

    /// <summary>
    /// Gets the global worker counter.
    /// </summary>
    public static int GlobalWorkerCounter => _globalWorkerCounter;
    
    /// <summary>
    /// Gets the ID (index) of the worker.
    /// </summary>
    public int WorkerId => _workerId;
    
    /// <summary>
    /// Gets a value indicating whether the pass loop is completed.
    /// </summary>
    public bool IsPassLoopCompleted => _isPassLoopCompleted;
    
    /// <summary>
    /// Gets the index of the best handled individual.
    /// </summary>
    public int BestHandledIndividualIndex => _bestHandledIndividualIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerController"/> class.
    /// </summary>
    /// <param name="workerId">The ID (index) of the worker.</param>
    /// <param name="algorithmExecutor">The algorithm executor.</param>
    /// <param name="workerPassLoopDoneHandler">The handler for when the worker pass loop is done.</param>
    public WorkerController(
        int workerId,
        IAlgorithmExecutor algorithmExecutor,
        IWorkerPassLoopDoneHandler? workerPassLoopDoneHandler = null)
    {
        _workerId = workerId;
        _workerThreadName = $"{Interlocked.Increment(ref _globalWorkerCounter)}-DEWorkerThread_{_workerId}";
        _algorithmExecutor = algorithmExecutor;
        _workerPassLoopDoneHandler = workerPassLoopDoneHandler;
    }

    /// <summary>
    /// Starts the worker.
    /// </summary>
    /// <param name="throwIfRunning">Indicates whether to throw an exception if the worker is already running.</param>
    public void Start(bool throwIfRunning = false)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                if (throwIfRunning)
                {
                    throw new InvalidOperationException("The worker is already running.");
                }
                
                return;
            }
            
            StartAndWaitUntilWorkerStarted();
        }
    }

    /// <summary>
    /// Stops the worker.
    /// </summary>
    /// <param name="throwIfStopped">Indicates whether to throw an exception if the worker is already stopped.</param>
    public void Stop(bool throwIfStopped = false)
    {
        lock (_lock)
        {
            if (_isRunning == false)
            {
                if (throwIfStopped)
                {
                    throw new InvalidOperationException("The worker is already stopped.");
                }
                
                return;
            }
            
            StopAndWaitUntilWorkerStopped();
        }
    }

    /// <summary>
    /// Permits the worker to start the pass loop.
    /// </summary>
    public void PermitToPassLoop()
    {
        _isPassLoopCompleted = false;
        _passLoopPermitted = true;
    }

    /// <summary>
    /// Runs the worker loop.
    /// </summary>
    private void RunWorkerLoop()
    {
        _isRunning = true;
        _isPreparingToRun = false;
        
        try
        {
            while (_workerShouldStop == false)
            {
                while (_passLoopPermitted == false && _workerShouldStop == false) ;
                _passLoopPermitted = false;

                if (_workerShouldStop)
                    break;

                _algorithmExecutor.Execute(_workerId,
                                           out var bestHandledIndividualIndex);
                _bestHandledIndividualIndex = bestHandledIndividualIndex;

                _isPassLoopCompleted = true;

                var shouldTerminate = false;
                _workerPassLoopDoneHandler?.Handle(this, out shouldTerminate);
                if (shouldTerminate)
                    break;
            }
        }
        catch (Exception ex)
        {
            _exception = ex;
            _workerPassLoopDoneHandler?.Handle(this, out _);
        }
        finally
        {
            _isRunning = false;
        }
    }
    
    /// <summary>
    /// Starts the worker and waits until it is started.
    /// </summary>
    private void StartAndWaitUntilWorkerStarted()
    {
        EnsureRunReadyState();
        
        _workerThread = new Thread(RunWorkerLoop)
        {
            Name = _workerThreadName,
            Priority = ThreadPriority.Highest
        };
        
        _workerThread.Start();
        
        var spinWait = new SpinWait();
        while (_isPreparingToRun)
            spinWait.SpinOnce();
    }

    /// <summary>
    /// Ensures the worker is in a ready state to run.
    /// </summary>
    private void EnsureRunReadyState()
    {
        _isPreparingToRun = true;
        _workerShouldStop = false;
        _exception = null;
        
        PermitToPassLoop();
    }

    /// <summary>
    /// Stops the worker and waits until it is stopped.
    /// </summary>
    private void StopAndWaitUntilWorkerStopped()
    {
        _workerShouldStop = true;

        var spinWait = new SpinWait();
        while (_isRunning)
            spinWait.SpinOnce();
    }

    /// <summary>
    /// Disposes the worker.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Disposes the worker.
    /// </summary>
    /// <param name="disposing">Indicates whether the worker is being disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        if (disposing)
        {
            lock (_lock)
            {
                StopAndWaitUntilWorkerStopped();
            }
            
            Interlocked.Decrement(ref _globalWorkerCounter);
        }

        _isDisposed = true;
    }
    
    /// <summary>
    /// Finalizes the worker.
    /// </summary>
    ~WorkerController()
    {
        Dispose(false);
    }
}
