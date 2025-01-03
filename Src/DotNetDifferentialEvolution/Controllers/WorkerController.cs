using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;

namespace DotNetDifferentialEvolution.Controllers;

public enum WorkerState
{
    AlgorithmExecution,
    AlgorithmExecutionDone,
    WaitingPermission,
    PermittedToPassLoop,
    ThrewException,
    Stopped
}

public class WorkerController : IDisposable
{
    public WorkerState State { get; private set; }
    
    private static volatile int _globalWorkerCounter = 0;
    
    private readonly object _lock = new();
    
    private bool _isDisposed;
    
    private readonly int _workerId;
    private readonly string _workerThreadName;

    private volatile bool _isPassLoopCompleted;
    private volatile bool _passLoopPermitted;

    private volatile bool _isRunning;
    private volatile bool _workerShouldStop;
    
    private volatile int _bestHandledIndividualIndex;
    
    private Thread? _workerThread;
    private Exception? _exception;

    private readonly IAlgorithmExecutor _algorithmExecutor;
    
    private readonly IWorkerPassLoopDoneHandler? _workerPassLoopDoneHandler;

    public bool IsRunning => _isRunning;
    
    public bool HasException => _exception != null;

    public Exception? Exception => _exception;

    public static int GlobalWorkerCounter => _globalWorkerCounter;
    
    public int WorkerId => _workerId;
    
    public bool IsPassLoopCompleted => _isPassLoopCompleted;
    
    public int BestHandledIndividualIndex => _bestHandledIndividualIndex;

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

    public void PermitToPassLoop()
    {
        _isPassLoopCompleted = false;
        _passLoopPermitted = true;
    }

    private void RunWorkerLoop()
    {
        try
        {
            _isRunning = true;
            
            while (_workerShouldStop == false)
            {
                State = WorkerState.WaitingPermission;
                while (_passLoopPermitted == false && _workerShouldStop == false) ;
                _passLoopPermitted = false;
                State = WorkerState.PermittedToPassLoop;

                if (_workerShouldStop)
                    break;

                State = WorkerState.AlgorithmExecution;
                _algorithmExecutor.Execute(_workerId,
                                           out var bestHandledIndividualIndex);
                _bestHandledIndividualIndex = bestHandledIndividualIndex;
                State = WorkerState.AlgorithmExecutionDone;

                _isPassLoopCompleted = true;

                var shouldTerminate = false;
                _workerPassLoopDoneHandler?.Handle(this, out shouldTerminate);
                if (shouldTerminate)
                    break;
            }
        }
        catch (Exception ex)
        {
            State = WorkerState.ThrewException;
            _exception = ex;
            _workerPassLoopDoneHandler?.Handle(this, out _);
        }
        finally
        {
            State = WorkerState.Stopped;
            _isRunning = false;
        }
    }
    
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
        while (_isRunning == false)
            spinWait.SpinOnce();
    }

    private void EnsureRunReadyState()
    {
        _workerShouldStop = false;
        _exception = null;
        
        PermitToPassLoop();
    }

    private void StopAndWaitUntilWorkerStopped()
    {
        _workerShouldStop = true;

        var spinWait = new SpinWait();
        while (_isRunning)
            spinWait.SpinOnce();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
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
    
    ~WorkerController()
    {
        Dispose(false);
    }
}
