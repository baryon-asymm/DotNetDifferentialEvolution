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

    private volatile bool _workerShouldStop;
    
    private volatile int _bestHandledIndividualIndex;
    
    private Thread? _workerThread;

    private readonly IAlgorithmExecutor _algorithmExecutor;
    
    private readonly IWorkerPassLoopDoneHandler? _workerPassLoopDoneHandler;

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

    public bool IsRunning()
    {
        lock (_lock)
        {
            return _workerThread?.IsAlive == true;
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            EnsureRunReadyState();

            _workerThread = new Thread(RunWorkerLoop)
            {
                Name = _workerThreadName,
                Priority = ThreadPriority.Highest
            };

            _workerThread.Start();
        }
    }

    public void Stop(bool waitUntilStopped = false)
    {
        lock (_lock)
        {
            EnsureRunningState();
            
            if (waitUntilStopped)
                StopAndWaitUntilWorkerStopped();
            else
                SendStopSignal();
        }
    }

    public void PermitToPassLoop()
    {
        _passLoopPermitted = true;
        _isPassLoopCompleted = false;
    }

    private void RunWorkerLoop()
    {
        while (_workerShouldStop == false)
        {
            while (_passLoopPermitted == false && _workerShouldStop == false) ;
            _passLoopPermitted = false;
            
            if (_workerShouldStop)
                return;

            _algorithmExecutor.Execute(_workerId,
                                       out var bestHandledIndividualIndex);
            _bestHandledIndividualIndex = bestHandledIndividualIndex;

            _isPassLoopCompleted = true;
            
            _workerPassLoopDoneHandler?.Handle(this);
        }
    }

    private void EnsureRunReadyState()
    {
        if (_workerThread != null && _workerThread.IsAlive)
            throw new InvalidOperationException("The worker is already running.");

        _workerShouldStop = false;
        
        PermitToPassLoop();
    }

    private void EnsureRunningState()
    {
        if (_workerThread == null || _workerThread.IsAlive == false)
            throw new InvalidOperationException("The worker is not running.");
    }

    private void StopAndWaitUntilWorkerStopped()
    {
        SendStopSignal();

        var spinWait = new SpinWait();
        while (_workerThread != null && _workerThread.IsAlive)
            spinWait.SpinOnce();
    }

    private void SendStopSignal()
    {
        _workerShouldStop = true;
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
