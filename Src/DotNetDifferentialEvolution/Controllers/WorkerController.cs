using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;

namespace DotNetDifferentialEvolution.Controllers;

public class WorkerController : IDisposable
{
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
            
            EnsureRunReadyState();
            
            _workerThread = new Thread(RunWorkerLoop)
            {
                Name = _workerThreadName,
                Priority = ThreadPriority.Highest
            };

            _workerThread.Start();
            _isRunning = true;
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
        _passLoopPermitted = true;
        _isPassLoopCompleted = false;
    }

    private void RunWorkerLoop()
    {
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
            StopAndWaitUntilWorkerStopped();
            
            Interlocked.Decrement(ref _globalWorkerCounter);
        }

        _isDisposed = true;
    }
    
    ~WorkerController()
    {
        Dispose(false);
    }
}
