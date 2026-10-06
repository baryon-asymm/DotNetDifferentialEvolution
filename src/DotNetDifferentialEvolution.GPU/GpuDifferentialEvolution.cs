using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// A built run: the population sampled and evaluated on the device, the kernels compiled.
/// <see cref="RunAsync"/> runs the generations on a thread of its own; the population stays on
/// the device and reaches the host only for the observer and once at the end.
/// </summary>
public sealed class GpuDifferentialEvolution : IDisposable
{
    private readonly AcceleratorLease _lease;
    private readonly RunSettings _settings;
    private readonly ulong _crossoverThreshold;
    private readonly double _pBestRateMin;
    private readonly List<MemoryBuffer> _allocated = [];
    private readonly PopulationTransfers _transfers = new();
    private readonly CancellationTokenSource _disposal = new();
    private readonly object _gate = new();
    private readonly KernelLauncher _launcher;
    private readonly GenerationBookkeeping _bookkeeping;
    private readonly List<RunState> _sinceStopRead = [];
    private RunState _state;
    private Task<GpuOptimizationResult>? _run;
    private Thread? _runThread;
    private bool _disposed;
    private bool _releaseWhenTheRunEnds;

    /// <summary>
    /// Builds the optimizer on an open lease, which it owns from this call on: it is disposed with
    /// the optimizer, or here if building fails.
    /// </summary>
    /// <param name="lease">The device.</param>
    /// <param name="settings">The validated settings.</param>
    /// <param name="compile">Compiles the kernels for the objective on the accelerator.</param>
    internal GpuDifferentialEvolution(AcceleratorLease lease, RunSettings settings, Func<Accelerator, KernelLauncher> compile)
    {
        _lease = lease;
        _settings = settings;
        var strategy = settings.Strategy;
        _crossoverThreshold = strategy.Rule == ParameterRule.Fixed ? DeStep.CrossoverThreshold(strategy.CrossoverProbability) : 0UL;
        _pBestRateMin = strategy.PBestRateMin(settings.PopulationSize);
        Device = new GpuDeviceInfo(KindOf(lease.Backend), lease.Accelerator.Name, lease.FallbackReason);

        var accelerator = lease.Accelerator;
        try
        {
            using var binding = accelerator.BindScoped();
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
        catch
        {
            ReleaseAll();
            throw;
        }
    }

    /// <summary>Gets the device the run is on, and why Auto skipped the devices before it.</summary>
    public GpuDeviceInfo Device { get; }

    /// <summary>Gets the number of population downloads so far (ACCEPTANCE.md, check 5b).</summary>
    internal int PopulationDownloadCount => _transfers.DownloadCount;

    /// <summary>Gets the number of stop-word reads so far (ACCEPTANCE.md, S17).</summary>
    internal int StopReadCount => _transfers.StopReadCount;

    /// <summary>Gets the number of evaluations so far: N after <c>Build</c> (ACCEPTANCE.md, check 1a).</summary>
    internal long EvaluationCount => _state.Evaluations;

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
            ObjectDisposedException.ThrowIf(_disposed, this);
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
    /// allocated: its device buffers, and the device itself unless it was the caller's.
    /// </summary>
    public void Dispose()
    {
        Thread? runThread;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            runThread = _runThread;
            if (runThread == Thread.CurrentThread)
            {
                // Called from the observer: the run thread frees everything once the loop has stopped.
                _releaseWhenTheRunEnds = true;
            }
        }

        _disposal.Cancel();
        if (runThread == Thread.CurrentThread)
        {
            return;
        }

        runThread?.Join();
        ReleaseAll();
    }

    private static GpuDevice KindOf(Backend backend) => backend switch
    {
        Backend.Cuda => GpuDevice.Cuda,
        Backend.OpenCL => GpuDevice.OpenCL,
        Backend.Cpu => GpuDevice.Cpu,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Not a defined backend."),
    };

    private static bool IsReportedToTheTask(Exception failure) => failure is not OutOfMemoryException;

    private void Run(TaskCompletionSource<GpuOptimizationResult> completion, CancellationToken cancellationToken)
    {
        try
        {
            using var binding = _lease.Accelerator.BindScoped();
            do
            {
                if (cancellationToken.IsCancellationRequested || _disposal.IsCancellationRequested)
                {
                    _ = completion.TrySetCanceled(cancellationToken.IsCancellationRequested ? cancellationToken : _disposal.Token);
                    return;
                }

                RunGeneration();
                var generation = _state.Generation;
                GenerationEnqueued?.Invoke(generation);
                var observerDue = _settings.Handler is not null && generation % _settings.EveryNGenerations == 0;
                if (_settings.Stagnation is not null
                    && (observerDue || generation % _settings.StopReadInterval == 0)
                    && Stopped())
                {
                    // As in the CPU package, the observer sees the generation the rule stops at, when it is due there:
                    // the stop word is read at every due generation, so an earlier stop was never due.
                    if (observerDue && _state.Generation == generation)
                    {
                        _settings.Handler!.Handle(Snapshot());
                    }

                    break;
                }

                if (observerDue)
                {
                    _settings.Handler!.Handle(Snapshot());
                }
            }
            while (!_settings.LimitReached(_state.Generation, _state.Evaluations));

            _ = completion.TrySetResult(Result());
        }
        catch (Exception failure) when (IsReportedToTheTask(failure))
        {
            _ = completion.TrySetException(failure);
        }
        finally
        {
            bool release;
            lock (_gate)
            {
                release = _releaseWhenTheRunEnds;
            }

            if (release)
            {
                ReleaseAll();
            }
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
    /// Reads the stop word. When the stagnation rule has fired, every kernel since has done nothing, and the host's
    /// counters go back to the generation it fired in, so the run ends as if the word had been read every generation.
    /// </summary>
    /// <returns>Whether the run stops.</returns>
    private bool Stopped()
    {
        var stop = new int[BookkeepingKernels.StopLength];
        _transfers.ReadStop(_lease.Accelerator, _bookkeeping.Views.Stop, stop);
        var states = _sinceStopRead.ToArray();
        _sinceStopRead.Clear();
        if (stop[BookkeepingKernels.StopSet] == 0)
        {
            return false;
        }

        _state = Array.Find(states, state => state.Generation == stop[BookkeepingKernels.StopGeneration])!;
        return true;
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
            _lease.Accelerator, views.Current.SubView(0, genes.Length), views.CurrentFitness.SubView(0, populationSize), genes, fitness);
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

    private MemoryBuffer1D<double, Stride1D.Dense> Allocate(Accelerator accelerator, long length)
    {
        var buffer = accelerator.Allocate1D<double>(length);
        _allocated.Add(buffer);
        return buffer;
    }

    private void ReleaseBuffers()
    {
        foreach (var buffer in _allocated)
        {
            buffer.Dispose();
        }

        _allocated.Clear();
    }

    private void ReleaseAll()
    {
        using (_lease.Accelerator.BindScoped())
        {
            ReleaseBuffers();

            // Null only when building failed before they were created.
            _bookkeeping?.Dispose();
            _launcher?.Dispose();
        }

        _lease.Dispose();
        _disposal.Dispose();
    }

    /// <summary>The host's counters after a generation, and where the population is.</summary>
    /// <param name="Generation">The generations run.</param>
    /// <param name="Evaluations">The evaluations so far.</param>
    /// <param name="PopulationSize">N of the next generation.</param>
    /// <param name="ArchiveCapacity">The archive's capacity for the next generation.</param>
    /// <param name="Views">The population, the current one in <c>Current</c>.</param>
    private sealed record RunState(int Generation, long Evaluations, int PopulationSize, int ArchiveCapacity, PopulationViews Views);
}
