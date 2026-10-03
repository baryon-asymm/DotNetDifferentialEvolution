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
    private readonly List<MemoryBuffer> _allocated = [];
    private readonly PopulationTransfers _transfers = new();
    private readonly CancellationTokenSource _disposal = new();
    private readonly object _gate = new();
    private readonly KernelLauncher _launcher;
    private PopulationViews _views;
    private int _generations;
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
        _crossoverThreshold = DeStep.CrossoverThreshold(settings.CrossoverProbability);
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

            _launcher = compile(accelerator);
            _views = new PopulationViews(
                currentGenes.View, currentFitness.View, nextGenes.View, nextFitness.View, trial.View, lowerBound.View, upperBound.View);
            _launcher.Initialize(Parameters(0), _views);
            accelerator.Synchronize();
            EvaluationCount = settings.PopulationSize;
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

    /// <summary>Gets the number of evaluations so far: N after <c>Build</c> (ACCEPTANCE.md, check 1a).</summary>
    internal long EvaluationCount { get; private set; }

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

                _generations++;
                _launcher.Generation(Parameters(_generations), _views);
                _views = _views.Swapped();
                EvaluationCount += _settings.PopulationSize;
                if (_settings.Handler is { } handler && _generations % _settings.EveryNGenerations == 0)
                {
                    handler.Handle(Snapshot());
                }
            }
            while (!_settings.LimitReached(_generations, EvaluationCount));

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

    private StepParameters Parameters(int generation) => new(
        _settings.Seed,
        generation,
        _settings.PopulationSize,
        _settings.GenomeSize,
        _settings.MutationForce,
        _crossoverThreshold);

    private (double[] Genes, double[] Fitness) DownloadCurrent()
    {
        var genes = new double[_settings.PopulationSize * _settings.GenomeSize];
        var fitness = new double[_settings.PopulationSize];
        _transfers.Download(_lease.Accelerator, _views.Current, _views.CurrentFitness, genes, fitness);
        return (genes, fitness);
    }

    private GpuPopulationSnapshot Snapshot()
    {
        var (genes, fitness) = DownloadCurrent();
        return new GpuPopulationSnapshot(_generations, EvaluationCount, _settings.PopulationSize, _settings.GenomeSize, genes, fitness);
    }

    private GpuOptimizationResult Result()
    {
        var (genes, fitness) = DownloadCurrent();
        var best = BestPick.IndexOf(fitness);
        var bestGenes = genes.AsSpan(best * _settings.GenomeSize, _settings.GenomeSize).ToArray();
        return new GpuOptimizationResult(bestGenes, fitness[best], _generations, EvaluationCount, Device);
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
        }

        _lease.Dispose();
        _disposal.Dispose();
    }
}
