using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces;

namespace DotNetDifferentialEvolution.Controllers;

/// <summary>
/// Manages the execution of a worker thread for the differential evolution algorithm.
/// </summary>
public class WorkerController : IDisposable
{
    private static volatile int ActiveWorkerCount;

    private readonly object _lock = new();

    private bool _isDisposed;
    private readonly string _workerThreadName;

    private volatile bool _isPassLoopCompleted;
    private volatile bool _passLoopPermitted;

    private volatile bool _isPreparingToRun;
    private volatile bool _isRunning;
    private volatile bool _workerShouldStop;

    private volatile int _bestHandledIndividualIndex;

    private Thread? _workerThread;
    private readonly IAlgorithmExecutor _algorithmExecutor;

    private readonly IWorkerPassLoopDoneHandler? _workerPassLoopDoneHandler;

    /// <summary>
    /// Gets a value indicating whether the worker is running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets a value indicating whether the worker has encountered an exception.
    /// </summary>
    public bool HasException => Exception != null;

    /// <summary>
    /// Gets the exception encountered by the worker, if any.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// Gets the global worker counter.
    /// </summary>
    public static int GlobalWorkerCounter => ActiveWorkerCount;

    /// <summary>
    /// Gets the ID (index) of the worker.
    /// </summary>
    public int WorkerId { get; }

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
        WorkerId = workerId;
        _workerThreadName = $"{Interlocked.Increment(ref ActiveWorkerCount)}-DEWorkerThread_{WorkerId}";
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
            if (!_isRunning)
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
            while (!_workerShouldStop)
            {
                // Cooperative wait for the next generation. A fresh SpinWait per generation so the
                // backoff always starts from zero; SpinOnce(-1) keeps SpinWait's spin/yield
                // progression but never escalates to Thread.Sleep(1), which would add up to a
                // millisecond to a barrier that is crossed once per generation. Yielding instead of
                // burning the core is what keeps an oversubscribed worker count (more workers than
                // available cores, e.g. UseAllProcessors) from collapsing into livelock.
                var passLoopSpinWait = new SpinWait();
                while (MustWaitForPassPermission())
                {
                    passLoopSpinWait.SpinOnce(sleep1Threshold: -1);
                }

                _passLoopPermitted = false;

                if (_workerShouldStop)
                {
                    break;
                }

                _algorithmExecutor.Execute(WorkerId,
                                           out var bestHandledIndividualIndex);
                _bestHandledIndividualIndex = bestHandledIndividualIndex;

                _isPassLoopCompleted = true;

                var shouldTerminate = false;
                _workerPassLoopDoneHandler?.Handle(this, out shouldTerminate);
                if (shouldTerminate)
                {
                    break;
                }
            }
        }
        // A worker thread is a failure boundary. Any exception from the user-supplied fitness
        // function must be captured and marshaled to the orchestrator (surfaced as an
        // AggregateException), never left to crash the thread, so every type but
        // OutOfMemoryException is caught (IsCapturedForTheOrchestrator).
        catch (Exception ex) when (IsCapturedForTheOrchestrator(ex))
        {
            Exception = ex;
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
    /// <summary>
    /// Whether the worker may not yet start its next pass. Both fields are volatile and written by
    /// other threads (the orchestrator's permit, <see cref="Stop"/>); keeping the test in a method
    /// of its own leaves no constant for a flow analysis to see in the spin-wait that polls it.
    /// </summary>
    private bool MustWaitForPassPermission() => !_passLoopPermitted && !_workerShouldStop;

    /// <summary>
    /// Whether a worker failure is captured and handed to the orchestrator. Every exception is,
    /// except <see cref="OutOfMemoryException"/>: a process out of memory cannot be trusted to
    /// marshal it, so it is left to end the thread as the runtime does by default.
    /// </summary>
    private static bool IsCapturedForTheOrchestrator(Exception exception) =>
        exception is not OutOfMemoryException;

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
        {
            spinWait.SpinOnce();
        }
    }

    /// <summary>
    /// Ensures the worker is in a ready state to run.
    /// </summary>
    private void EnsureRunReadyState()
    {
        _isPreparingToRun = true;
        _workerShouldStop = false;
        Exception = null;

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
        {
            spinWait.SpinOnce();
        }
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
        {
            return;
        }

        if (disposing)
        {
            lock (_lock)
            {
                StopAndWaitUntilWorkerStopped();
            }

            _ = Interlocked.Decrement(ref ActiveWorkerCount);
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
