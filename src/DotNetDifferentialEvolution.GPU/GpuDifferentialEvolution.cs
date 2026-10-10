using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Util;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// A built run: the population sampled and evaluated on the device, the kernels compiled.
/// <see cref="RunAsync"/> runs the generations on a thread of its own; the population stays on
/// the device and reaches the host only for the observer and once at the end.
/// </summary>
public sealed class GpuDifferentialEvolution : IDisposable
{
    private readonly RunSettings _settings;
    private readonly ulong _crossoverThreshold;
    private readonly double _pBestRateMin;
    private readonly List<IDisposable> _owned = [];
    private readonly PopulationTransfers _transfers = new();
    private readonly CancellationTokenSource _disposal = new();
    private readonly object _gate = new();
    private readonly KernelLauncher _launcher;
    private readonly GenerationBookkeeping _bookkeeping;
    private readonly PageLockedArray1D<int>? _stopCopy;
    private readonly List<RunState> _sinceStopRead = [];
    private RunState _state;
    private Task<GpuOptimizationResult>? _run;
    private Thread? _runThread;
    private Thread? _disposingThread;
    private bool _disposeRequested;
    private bool _released;
    private bool _releaseWhenTheRunEnds;
    private bool _stopCopyPending;
    private int _stopCopyGeneration;
    private GpuOptimizationResult? _lastResult;

    /// <summary>
    /// Builds the optimizer on an open lease, which it owns from this call on: it is disposed with
    /// the optimizer, or here if building fails.
    /// </summary>
    /// <param name="lease">The device.</param>
    /// <param name="settings">The validated settings.</param>
    /// <param name="compile">Compiles the kernels for the objective on the accelerator.</param>
    /// <param name="plantedRelease">
    /// Creates, first of all the optimizer allocates, a release of the tests' choosing on the accelerator (ACCEPTANCE.md, A6);
    /// <see langword="null"/> outside the tests.
    /// </param>
    internal GpuDifferentialEvolution(
        AcceleratorLease lease,
        RunSettings settings,
        Func<Accelerator, KernelLauncher> compile,
        Func<Accelerator, IDisposable>? plantedRelease = null)
    {
        Lease = lease;
        _settings = settings;
        var strategy = settings.Strategy;
        _crossoverThreshold = strategy.Rule == ParameterRule.Fixed ? DeStep.CrossoverThreshold(strategy.CrossoverProbability) : 0UL;
        _pBestRateMin = strategy.PBestRateMin(settings.PopulationSize);
        Device = new GpuDeviceInfo(KindOf(lease.Backend), lease.Accelerator.Name, lease.FallbackReason);

        var accelerator = lease.Accelerator;
        try
        {
            using var binding = accelerator.BindScoped();
            if (plantedRelease is not null)
            {
                _owned.Add(plantedRelease(accelerator));
            }

            var geneCount = (long)settings.PopulationSize * settings.GenomeSize;
            var currentGenes = Allocate(accelerator, geneCount);
            var currentFitness = Allocate(accelerator, settings.PopulationSize);
            var nextGenes = Allocate(accelerator, geneCount);
            var nextFitness = Allocate(accelerator, settings.PopulationSize);
            var trial = Allocate(accelerator, geneCount);
            var lowerBound = Allocate(accelerator, settings.GenomeSize);
            var upperBound = Allocate(accelerator, settings.GenomeSize);
            PopulationTransfers.Upload(lowerBound, settings.LowerBound);
            PopulationTransfers.Upload(upperBound, settings.UpperBound);
            if (settings.Stagnation is not null)
            {
                _stopCopy = accelerator.AllocatePageLocked1D<int>(BookkeepingKernels.StopLength);
            }

            var archiveCapacity = ArchiveRules.Capacity(strategy.ArchiveSizeRate, settings.PopulationSize);
            _bookkeeping = new GenerationBookkeeping(
                accelerator,
                new BookkeepingPlan(
                    settings.PopulationSize,
                    settings.GenomeSize,
                    strategy.Scheme,
                    strategy.Rule,
                    archiveCapacity,
                    strategy.MemorySize,
                    strategy.AdaptationRate,
                    strategy.LShadeBudget is not null,
                    strategy.MutationForce,
                    strategy.CrossoverProbability,
                    settings.Stagnation),
                settings.Seed);
            _launcher = compile(accelerator);
            var views = new PopulationViews(
                currentGenes.View, currentFitness.View, nextGenes.View, nextFitness.View, trial.View, lowerBound.View, upperBound.View);
            _launcher.Initialize(Parameters(0, settings.PopulationSize), views);
            _bookkeeping.AfterInitialization(views);
            accelerator.Synchronize();
            _state = new RunState(0, settings.PopulationSize, settings.PopulationSize, archiveCapacity, views);
        }
        catch (Exception original)
        {
            // Every release runs; the failures ride on the exception that made them necessary, which is thrown as it was
            // (ACCEPTANCE.md, A6).
            ReleaseFailures.Attach(original, ReleaseAll());
            throw;
        }
    }

    /// <summary>Gets the device the run is on, and why Auto skipped the devices before it.</summary>
    public GpuDeviceInfo Device { get; }

    /// <summary>
    /// Gets what the run left: <see langword="null"/> until a run ends. A run that completes leaves its result, the same object
    /// the task returns. A run that ends canceled, by the token or by <see cref="Dispose"/>, leaves the best individual of the
    /// population at the generation it stopped at, with that generation's counts: the lowest fitness, a <c>NaN</c> worst, a tie
    /// to the lowest index, as the result's rule; one synchronised download on the run's thread. A run that faults leaves
    /// <see langword="null"/>, also when the download of a canceled run fails. It is set before the task completes and before
    /// anything is released, so that any thread that observes the task's completion, or the end of <see cref="Dispose"/>, sees
    /// it.
    /// </summary>
    /// <value>The result of the run that ended, or <see langword="null"/>.</value>
    public GpuOptimizationResult? LastResult => Volatile.Read(ref _lastResult);

    /// <summary>Gets the number of population downloads so far (ACCEPTANCE.md, check 5b).</summary>
    internal int PopulationDownloadCount => _transfers.DownloadCount;

    /// <summary>Gets the number of synchronising stop-word reads so far (ACCEPTANCE.md, S17, A13).</summary>
    internal int StopReadCount => _transfers.StopReadCount;

    /// <summary>Gets the number of stop-word copies enqueued without a synchronisation so far (ACCEPTANCE.md, A13).</summary>
    internal int StopCopyCount => _transfers.StopCopyCount;

    /// <summary>Gets the number of evaluations so far: N after <c>Build</c> (ACCEPTANCE.md, check 1a).</summary>
    internal long EvaluationCount => _state.Evaluations;

    /// <summary>Gets the lease: for the tests of check A6, which look at the context it owns.</summary>
    internal AcceleratorLease Lease { get; }

    /// <summary>
    /// Gets every buffer and kernel the optimizer allocated and has not yet released: for the tests of check A7, which read
    /// their <c>IsDisposed</c> after <see cref="Dispose"/>, from a list taken before it.
    /// </summary>
    internal IReadOnlyList<DisposeBase> Allocated
    {
        get
        {
            List<DisposeBase> all = [.. _owned.OfType<DisposeBase>(), .. _bookkeeping.Allocated, .. _launcher.Allocated];
            if (_stopCopy is not null)
            {
                all.Add(_stopCopy);
            }

            return all;
        }
    }

    /// <summary>Gets a value indicating whether <see cref="Dispose"/> has been called: for the tests of checks A7 and A8.</summary>
    internal bool DisposeRequested
    {
        get
        {
            lock (_gate)
            {
                return _disposeRequested;
            }
        }
    }

    /// <summary>
    /// Gets or sets a hook called on the thread of a second <see cref="Dispose"/> just before it waits for the first to finish,
    /// outside the lock: the tests' way to know a second caller is waiting (ACCEPTANCE.md, A8). <see langword="null"/> outside
    /// the tests.
    /// </summary>
    internal Action? SecondDisposeWaiting { get; set; }

    /// <summary>
    /// Gets or sets a hook called on the run's thread after each generation is enqueued, with its number, before the
    /// stop word is read: the tests' way to act between a stop and its read (ACCEPTANCE.md, S18). <see langword="null"/>
    /// outside the tests.
    /// </summary>
    internal Action<int>? GenerationEnqueued { get; set; }

    /// <summary>
    /// Starts the run on a thread of its own and returns at once. The token is observed between
    /// generations and ends the task as canceled. After the run, a second call returns the same
    /// task.
    /// </summary>
    /// <param name="cancellationToken">Stops the run between generations.</param>
    /// <returns>The run's task: the best individual of the final population.</returns>
    /// <exception cref="InvalidOperationException">A run is in progress.</exception>
    /// <exception cref="ObjectDisposedException">The optimizer is disposed.</exception>
    public Task<GpuOptimizationResult> RunAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            if (_run is { } started)
            {
                return started.IsCompleted
                    ? started
                    : throw new InvalidOperationException(
                        "A run is in progress; RunAsync may be called again only after it ends, and then returns the same task.");
            }

            var completion = new TaskCompletionSource<GpuOptimizationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _run = completion.Task;
            _runThread = new Thread(() => Run(completion, cancellationToken))
            {
                IsBackground = true,
                Name = nameof(GpuDifferentialEvolution) + " run",
            };
            _runThread.Start();
            return _run;
        }
    }

    /// <summary>
    /// Stops a run in progress between generations, waits for it, and frees what the optimizer
    /// allocated: its device buffers, and the device itself unless it was the caller's. Every release runs even when one
    /// throws; the failures are then thrown together. A second call, also a concurrent one, returns once the first has
    /// stopped the run and released everything, and throws nothing.
    /// </summary>
    /// <exception cref="AggregateException">A release failed; everything else was released.</exception>
    public void Dispose()
    {
        Thread? runThread;
        bool first;
        lock (_gate)
        {
            var current = Thread.CurrentThread;
            runThread = _runThread;
            first = !_disposeRequested;
            if (first)
            {
                _disposeRequested = true;
                _disposingThread = current;
                if (runThread == current)
                {
                    // Called from the observer: the run thread frees everything once the loop has stopped.
                    _releaseWhenTheRunEnds = true;
                }
            }
            else if (_released || current == runThread || current == _disposingThread)
            {
                // Done already, or the caller is the thread doing, or holding up, the first call's work: waiting would be a deadlock.
                return;
            }
        }

        if (!first)
        {
            WaitUntilReleased();
            return;
        }

        _disposal.Cancel();
        if (runThread == Thread.CurrentThread)
        {
            return;
        }

        runThread?.Join();
        ReleaseFailures.ThrowIfAny(ReleaseAndSignal());
    }

    private static GpuDevice KindOf(Backend backend) => backend switch
    {
        Backend.Cuda => GpuDevice.Cuda,
        Backend.OpenCL => GpuDevice.OpenCL,
        Backend.Cpu => GpuDevice.Cpu,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Not a defined backend."),
    };

    /// <summary>Every exception, of whatever type, is a failure of the run and faults its task (ACCEPTANCE.md, A8).</summary>
    private static bool IsAFailureOfTheRun(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return true;
    }

    private static AggregateException Combine(Exception? runFailure, List<Exception> releaseFailures)
    {
        List<Exception> all = [];
        if (runFailure is not null)
        {
            all.Add(runFailure);
        }

        all.AddRange(releaseFailures);
        return new AggregateException(all).Flatten();
    }

    /// <summary>
    /// The run thread: the loop, then, when <see cref="Dispose"/> was called from the observer, the release; the task is
    /// completed last, so that a release failure faults it and never leaves the thread.
    /// </summary>
    private void Run(TaskCompletionSource<GpuOptimizationResult> completion, CancellationToken cancellationToken)
    {
        var outcome = Execute(cancellationToken);
        bool release;
        lock (_gate)
        {
            release = _releaseWhenTheRunEnds;
        }

        var releaseFailures = release ? ReleaseAndSignal() : [];
        _ = releaseFailures.Count > 0 ? completion.TrySetException(Combine(outcome.Failure, releaseFailures))
            : outcome.Failure is not null ? completion.TrySetException(outcome.Failure)
            : outcome.Result is not null ? completion.TrySetResult(outcome.Result)
            : completion.TrySetCanceled(outcome.CanceledBy);
    }

    /// <summary>The generations, until a limit, the stop rule, the token, <see cref="Dispose"/> or a failure of any kind; never throws.</summary>
    private RunOutcome Execute(CancellationToken cancellationToken)
    {
        try
        {
            using var binding = Lease.Accelerator.BindScoped();
            var stopped = false;
            do
            {
                if (cancellationToken.IsCancellationRequested || _disposal.IsCancellationRequested)
                {
                    // As in the CPU package, the stop rule is tested before the cancellation: a rule that fired before
                    // the request, between two looks at the stop word, ends the run with its result (ACCEPTANCE.md, S18).
                    if (_settings.Stagnation is not null && ReadStop())
                    {
                        stopped = true;
                        break;
                    }

                    // The best individual of the generation the run stops at is kept before the task is completed and before
                    // anything is released (ACCEPTANCE.md, A16); a download that fails faults the run below.
                    Volatile.Write(ref _lastResult, Result());
                    return RunOutcome.Canceled(cancellationToken.IsCancellationRequested ? cancellationToken : _disposal.Token);
                }

                RunGeneration();
                var generation = _state.Generation;
                GenerationEnqueued?.Invoke(generation);
                var observerDue = _settings.Handler is not null && generation % _settings.EveryNGenerations == 0;
                if (_settings.Stagnation is not null)
                {
                    if (observerDue)
                    {
                        // The observer synchronises anyway: the word is read, with a synchronisation, before each of its calls.
                        if (ReadStop())
                        {
                            // As in the CPU package, the observer sees the generation the rule stops at, when it is due there:
                            // the stop word is read at every due generation, so an earlier stop was never due.
                            if (_state.Generation == generation)
                            {
                                _settings.Handler!.Handle(Snapshot());
                            }

                            stopped = true;
                            break;
                        }
                    }
                    else if (generation % _settings.StopReadInterval == 0 && StopCopyShowsTheRuleFired())
                    {
                        // The copy taken an interval ago says the rule has fired: the end of the loop reads the word for good.
                        break;
                    }
                }

                if (observerDue)
                {
                    _settings.Handler!.Handle(Snapshot());
                }
            }
            while (!_settings.LimitReached(_state.Generation, _state.Evaluations));

            // The end of every run that has not read the word since: a limit beside the rule (RunSettings allows it; the public
            // builder does not) or a copy that showed the rule fired. A rule that fired before ends the run at its own
            // generation (ACCEPTANCE.md, S18).
            if (!stopped && _settings.Stagnation is not null)
            {
                _ = ReadStop();
            }

            var result = Result();
            Volatile.Write(ref _lastResult, result);
            return RunOutcome.Completed(result);
        }
        catch (Exception failure) when (IsAFailureOfTheRun(failure))
        {
            return RunOutcome.Failed(failure);
        }
    }

    /// <summary>
    /// Enqueues one generation and the bookkeeping after it, and advances the host's counters: the generation, the
    /// evaluations, and under L-SHADE the population size and the archive's capacity, which the host computes.
    /// </summary>
    private void RunGeneration()
    {
        var generation = _state.Generation + 1;
        var populationSize = _state.PopulationSize;
        var views = _state.Views;
        _launcher.Generation(Parameters(generation, populationSize), views, _bookkeeping.Views);
        views = views.Swapped();
        var evaluations = _state.Evaluations + populationSize;

        var strategy = _settings.Strategy;
        var nextPopulationSize = strategy.LShadeBudget is { } budget
            ? LShadeSchedule.NextPopulationSize(_settings.PopulationSize, budget, evaluations, populationSize)
            : populationSize;
        var nextArchiveCapacity = nextPopulationSize < populationSize
            ? ArchiveRules.Capacity(strategy.ArchiveSizeRate, nextPopulationSize)
            : _state.ArchiveCapacity;
        _bookkeeping.AfterGeneration(ref views, generation, populationSize, _state.ArchiveCapacity, nextPopulationSize, nextArchiveCapacity);
        _state = new RunState(generation, evaluations, nextPopulationSize, nextArchiveCapacity, views);
        if (_settings.Stagnation is not null)
        {
            _sinceStopRead.Add(_state);
        }
    }

    /// <summary>
    /// Reads the stop word, synchronising the accelerator. When the stagnation rule has fired, every kernel since has done
    /// nothing, and the host's counters go back to the generation it fired in, so the run ends as if the word had been read
    /// every generation.
    /// </summary>
    /// <returns>Whether the run stops.</returns>
    private bool ReadStop()
    {
        var stop = new int[BookkeepingKernels.StopLength];
        _transfers.ReadStop(Lease.Accelerator, _bookkeeping.Views.Stop, stop);

        // The synchronisation has landed any copy in flight, and the word read is the newest there is.
        _stopCopyPending = false;
        var states = _sinceStopRead.ToArray();
        _sinceStopRead.Clear();
        if (stop[BookkeepingKernels.StopSet] == 0)
        {
            return false;
        }

        _state = Array.Find(states, state => state.Generation == stop[BookkeepingKernels.StopGeneration])!;
        return true;
    }

    /// <summary>
    /// At a read interval, looks at the stop word copied an interval ago, without waiting for it, and copies the word again,
    /// without synchronising the accelerator: the copy lands in stream order, behind the generations enqueued so far, and is
    /// read at the next interval. A copy that has not landed by then tells nothing and is waited for another interval.
    /// </summary>
    /// <returns>Whether the earlier copy showed the rule had fired.</returns>
    private bool StopCopyShowsTheRuleFired()
    {
        var copy = _stopCopy!;
        if (_stopCopyPending)
        {
            switch (PopulationTransfers.PollStopCopy(copy))
            {
                case StopCopyState.Set:
                    return true;
                case StopCopyState.InFlight:
                    return false;
                case StopCopyState.NotSet:
                    // The rule had not fired by the generation the copy was taken at, so no state up to it is a stopping one.
                    _ = _sinceStopRead.RemoveAll(state => state.Generation <= _stopCopyGeneration);
                    _stopCopyPending = false;
                    break;
                default:
                    throw new InvalidOperationException("Not a defined stop copy state.");
            }
        }

        _transfers.BeginStopCopy(Lease.Accelerator.DefaultStream, _bookkeeping.Views.Stop, copy);
        _stopCopyGeneration = _state.Generation;
        _stopCopyPending = true;
        return false;
    }

    private StepParameters Parameters(int generation, int populationSize)
    {
        var strategy = _settings.Strategy;
        return new StepParameters(
            _settings.Seed,
            generation,
            populationSize,
            _settings.GenomeSize,
            strategy.MutationForce,
            _crossoverThreshold,
            strategy.Scheme,
            strategy.Rule,
            strategy.AcceptsTies ? TieRule.Accepted : TieRule.Refused,
            _pBestRateMin,
            strategy.PBestRate,
            strategy.MemorySize);
    }

    private (double[] Genes, double[] Fitness) DownloadCurrent()
    {
        var populationSize = _state.PopulationSize;
        var genomeSize = _settings.GenomeSize;
        var genes = new double[populationSize * genomeSize];
        var fitness = new double[populationSize];
        var views = _state.Views;
        _transfers.Download(
            Lease.Accelerator, views.Current.SubView(0, genes.Length), views.CurrentFitness.SubView(0, populationSize), genes, fitness);
        return (genes, fitness);
    }

    private GpuPopulationSnapshot Snapshot()
    {
        var (genes, fitness) = DownloadCurrent();
        return new GpuPopulationSnapshot(_state.Generation, _state.Evaluations, _state.PopulationSize, _settings.GenomeSize, genes, fitness);
    }

    private GpuOptimizationResult Result()
    {
        var (genes, fitness) = DownloadCurrent();
        var best = BestPick.IndexOf(fitness);
        var bestGenes = genes.AsSpan(best * _settings.GenomeSize, _settings.GenomeSize).ToArray();
        return new GpuOptimizationResult(bestGenes, fitness[best], _state.Generation, _state.Evaluations, Device);
    }

    private void WaitUntilReleased()
    {
        SecondDisposeWaiting?.Invoke();
        lock (_gate)
        {
            while (!_released)
            {
                _ = Monitor.Wait(_gate);
            }
        }
    }

    private MemoryBuffer1D<double, Stride1D.Dense> Allocate(Accelerator accelerator, long length)
    {
        var buffer = accelerator.Allocate1D<double>(length);
        _owned.Add(buffer);
        return buffer;
    }

    /// <summary>Releases everything and wakes the callers of <see cref="Dispose"/> that wait for it; never throws.</summary>
    /// <returns>The failures of the releases; empty when every release succeeded.</returns>
    private List<Exception> ReleaseAndSignal()
    {
        var failures = ReleaseAll();
        lock (_gate)
        {
            _released = true;
            Monitor.PulseAll(_gate);
        }

        return failures;
    }

    /// <summary>
    /// Releases the buffers, the bookkeeping, the kernels, the lease and the cancellation source, each in its own <c>try</c>
    /// (ACCEPTANCE.md, A6); a stop-word copy still in flight is waited for first, since its page-locked memory is freed.
    /// </summary>
    /// <returns>The failures, in release order; empty when every release succeeded.</returns>
    private List<Exception> ReleaseAll()
    {
        var failures = new List<Exception>();

        // Disposing an accelerator unbinds the thread whichever accelerator it is bound to, so what the caller's thread was
        // bound to is bound again after the lease is disposed (ACCEPTANCE.md, A11).
        var bound = Accelerator.Current;
        ScopedAcceleratorBinding? binding = null;
        try
        {
            binding = Lease.Accelerator.BindScoped();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter; the releases follow unbound, as far as they can.
        }

        if (_stopCopyPending)
        {
            try
            {
                Lease.Accelerator.Synchronize();
            }
            catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
            {
                // Collected by the filter.
            }

            _stopCopyPending = false;
        }

        ReleaseFailures.Run(_owned, failures);
        _owned.Clear();
        try
        {
            _stopCopy?.Dispose();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        // The bookkeeping and the launcher are null only when building failed before they were created.
        try
        {
            _bookkeeping?.Dispose();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        try
        {
            _launcher?.Dispose();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        try
        {
            binding?.Dispose();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        try
        {
            Lease.Dispose();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        try
        {
            if (bound is { IsDisposed: false })
            {
                bound.Bind();
            }
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        try
        {
            _disposal.Dispose();
        }
        catch (Exception failure) when (ReleaseFailures.Collect(failure, failures))
        {
            // Collected by the filter.
        }

        return failures;
    }

    /// <summary>The host's counters after a generation, and where the population is.</summary>
    /// <param name="Generation">The generations run.</param>
    /// <param name="Evaluations">The evaluations so far.</param>
    /// <param name="PopulationSize">N of the next generation.</param>
    /// <param name="ArchiveCapacity">The archive's capacity for the next generation.</param>
    /// <param name="Views">The population, the current one in <c>Current</c>.</param>
    private sealed record RunState(int Generation, long Evaluations, int PopulationSize, int ArchiveCapacity, PopulationViews Views);

    /// <summary>How the run ended, before its task is completed.</summary>
    /// <param name="Result">The result, when it completed.</param>
    /// <param name="Failure">The exception, when it failed.</param>
    /// <param name="CanceledBy">The token that ended it, when neither of the others.</param>
    private sealed record RunOutcome(GpuOptimizationResult? Result, Exception? Failure, CancellationToken CanceledBy)
    {
        public static RunOutcome Completed(GpuOptimizationResult result) => new(result, null, default);

        public static RunOutcome Failed(Exception failure) => new(null, failure, default);

        public static RunOutcome Canceled(CancellationToken token) => new(null, null, token);
    }
}
