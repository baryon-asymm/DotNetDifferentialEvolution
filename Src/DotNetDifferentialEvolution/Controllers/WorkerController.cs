using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.Controllers;

public class WorkerController : IDisposable
{
    private static volatile int _workerIdCounter = -1;

    private readonly object _lock = new();

    private readonly IWorkerExecutor _workerExecutor;

    private readonly string _workerThreadName;
    private volatile bool _isPassLoopCompleted;

    private volatile bool _passLoopPermitted;

    private readonly int _workerId;

    private volatile bool _workerShouldStop;
    private Thread? _workerThread;

    public WorkerController(
        IWorkerExecutor workerExecutor)
    {
        _workerId = Interlocked.Increment(ref _workerIdCounter);
        _workerExecutor = workerExecutor;
        _workerThreadName = $"DEWorkerThread_{_workerId}";
    }

    public bool IsPassLoopCompleted => _isPassLoopCompleted;

    public void Dispose()
    {
        // dispose

        Interlocked.Decrement(ref _workerIdCounter);
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

    public void Stop()
    {
        lock (_lock)
        {
            EnsureRunningState();
            StopAndWaitUntilWorkerStopped();
        }
    }

    public void PermitPassLoop()
    {
        _passLoopPermitted = true;
        _isPassLoopCompleted = false;
    }

    private void RunWorkerLoop()
    {
        while (_workerShouldStop == false)
        {
            while (_passLoopPermitted == false) ;
            _passLoopPermitted = false;

            //_workerExecutor.Execute(_workerId,
            //                        out _);

            _isPassLoopCompleted = true;
        }
    }

    private void EnsureRunReadyState()
    {
        if (_workerThread != null && _workerThread.IsAlive)
            throw new InvalidOperationException("The worker is already running.");

        _passLoopPermitted = false;
        _isPassLoopCompleted = false;
    }

    private void EnsureRunningState()
    {
        if (_workerThread == null || _workerThread.IsAlive == false)
            throw new InvalidOperationException("The worker is not running.");
    }

    private void StopAndWaitUntilWorkerStopped()
    {
        _workerShouldStop = true;

        var spinWait = new SpinWait();
        while (_workerThread != null && _workerThread.IsAlive)
            spinWait.SpinOnce();

        _workerShouldStop = false;
    }
}
