using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The device state of one run's configuration and the passes that keep it between generations, enqueued on the
/// accelerator's default stream in the CPU package's order (BOOT.md): the ranking, the archive, the adaptation, L-SHADE's
/// reduction, the best index, the stop rule. Allocates only what the configuration uses and loads each kernel on its
/// first use, through <see cref="KernelLoader"/>; a configuration with nothing to keep loads none.
/// </summary>
internal sealed class GenerationBookkeeping : IDisposable
{
    /// <summary>The largest N ranked by counting; above it, the bitonic network.</summary>
    public const int CountingRankLimit = 8192;

    private readonly Accelerator _accelerator;
    private readonly AcceleratorStream _stream;
    private readonly BookkeepingPlan _plan;
    private readonly int _seed;
    private readonly List<MemoryBuffer> _buffers = [];
    private readonly List<Kernel> _kernels = [];
    private readonly ArrayView<int> _partialIndices;
    private readonly ArrayView<int> _counts;
    private readonly ArrayView<int> _owners;
    private readonly ArrayView<double> _partials;
    private readonly ArrayView<double> _sortKeys;
    private readonly ArrayView<int> _memoryIndex;
    private readonly ArrayView<double> _lastBest;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, int>? _fillInts;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, double>? _fillDoubles;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>>? _bestOfChunks;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>, ArrayView<int>>? _bestOfPartials;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>>? _rankByCounting;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<double>, ArrayView<int>, ArrayView<int>>? _loadSortKeys;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<int>, int, int, ArrayView<int>>? _bitonicStep;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, int, ArrayView<int>, ArrayView<int>>? _countImproved;
    private Action<AcceleratorStream, Index1D, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>>? _scanImproved;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>, int, int, ArrayView<int>>? _placeImproved;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, ArrayView<double>, ArrayView<double>, int, ArrayView<int>>? _copyToArchive;
    private Action<AcceleratorStream, Index1D, ParameterRule, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>>? _sumSuccesses;
    private Action<AcceleratorStream, Index1D, ParameterRule, int, ArrayView<double>, double, int, MemoryRule, ArrayView<double>, ArrayView<int>, ArrayView<int>>? _adapt;
    private Action<AcceleratorStream, Index1D, PopulationViews, int, StrategyViews, int>? _compact;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<int>, ArrayView<double>, double, int, int, ArrayView<int>>? _stagnate;

    /// <summary>Allocates the configuration's buffers and writes their initial state.</summary>
    /// <param name="accelerator">The accelerator, bound to the calling thread.</param>
    /// <param name="plan">What the configuration keeps.</param>
    /// <param name="seed">The run's seed, for the archive's slot draws.</param>
    public GenerationBookkeeping(Accelerator accelerator, BookkeepingPlan plan, int seed)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(plan);
        _accelerator = accelerator;
        _stream = accelerator.DefaultStream;
        _plan = plan;
        _seed = seed;
        try
        {
            var populationSize = plan.PopulationSize;
            var chunks = BookkeepingKernels.ChunkCount(populationSize);
            var rankingLength = plan.NeedsRanking ? SortLength(populationSize) : 1;
            var archiveCapacity = Math.Max(plan.ArchiveCapacity, 0);
            var perIndividual = plan.Rule == ParameterRule.Fixed ? 1 : populationSize;
            var adaptationLength = plan.Rule switch
            {
                ParameterRule.Jade => 2,
                ParameterRule.Shade => 2 * plan.MemorySize,
                ParameterRule.Fixed or ParameterRule.Jde => 1,
                _ => 1,
            };

            var stop = Ints(BookkeepingKernels.StopLength);
            var bestIndex = Ints(1);
            var ranking = Ints(rankingLength);
            var archive = Doubles(Math.Max(1L, (long)archiveCapacity * plan.GenomeSize));
            var archiveSize = Ints(2);
            var adaptation = Doubles(adaptationLength);
            var forces = Doubles(perIndividual);
            var crossovers = Doubles(perIndividual);
            var outcomes = Ints(perIndividual);
            Views = new StrategyViews(stop, bestIndex, ranking, archive, archiveSize, adaptation, forces, crossovers, outcomes);
            _partialIndices = Ints(plan.NeedsBestIndex ? chunks : 1);
            _counts = Ints(archiveCapacity > 0 ? chunks : 1);
            _owners = Ints(Math.Max(1, archiveCapacity));
            _partials = Doubles(plan.Adapts ? (long)chunks * SuccessSums.Width : 1);
            _sortKeys = Doubles(plan.NeedsRanking && populationSize > CountingRankLimit ? rankingLength : 1);
            _memoryIndex = Ints(1);
            _lastBest = Doubles(1);

            FillInts(stop, 0);
            FillInts(bestIndex, 0);
            FillInts(archiveSize, 0);
            FillInts(_owners, -1);
            FillInts(_memoryIndex, 0);
            FillDoubles(_lastBest, StagnationRule.InitialLastBest);
            FillDoubles(adaptation, AdaptationRules.InitialValue);
            if (plan.Rule == ParameterRule.Jde)
            {
                FillDoubles(forces, plan.InitialMutationForce);
                FillDoubles(crossovers, plan.InitialCrossoverProbability);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the device state the generation kernel reads and writes.</summary>
    public StrategyViews Views { get; }

    /// <summary>
    /// The state the first generation needs from the initial population, as the CPU builder computes it: its ranking
    /// and its best index.
    /// </summary>
    /// <param name="views">The population, its initial individuals in <c>Current</c>.</param>
    public void AfterInitialization(PopulationViews views)
    {
        if (_plan.NeedsRanking)
        {
            Rank(views.CurrentFitness, _plan.PopulationSize);
        }

        if (_plan.NeedsBestIndex)
        {
            FindBest(views.CurrentFitness, _plan.PopulationSize);
        }
    }

    /// <summary>
    /// The passes after generation <paramref name="generation"/>, the views already swapped (the new population in
    /// <c>Current</c>, the parents in <c>Next</c>): the ranking, the archive, the adaptation, the reduction to
    /// <paramref name="nextPopulationSize"/>, the best index, the stop rule.
    /// </summary>
    /// <param name="views">The population; swapped again here when it is reduced.</param>
    /// <param name="generation">The generation just run.</param>
    /// <param name="populationSize">N of that generation.</param>
    /// <param name="archiveCapacity">The archive's capacity during that generation.</param>
    /// <param name="nextPopulationSize">N of the next generation; below N only under L-SHADE.</param>
    /// <param name="nextArchiveCapacity">The archive's capacity for that size.</param>
    public void AfterGeneration(
        ref PopulationViews views,
        int generation,
        int populationSize,
        int archiveCapacity,
        int nextPopulationSize,
        int nextArchiveCapacity)
    {
        if (_plan.NeedsRanking)
        {
            Rank(views.CurrentFitness, populationSize);
        }

        if (archiveCapacity > 0)
        {
            UpdateArchive(views.Next, generation, populationSize, archiveCapacity);
        }

        if (_plan.Adapts)
        {
            Adapt(views, populationSize);
        }

        if (nextPopulationSize < populationSize)
        {
            _compact ??= Load<Action<AcceleratorStream, Index1D, PopulationViews, int, StrategyViews, int>>(nameof(BookkeepingKernels.Compact));
            _compact(_stream, nextPopulationSize, views, _plan.GenomeSize, Views, nextArchiveCapacity);
            views = views.Swapped();
        }

        if (_plan.NeedsBestIndex)
        {
            FindBest(views.CurrentFitness, nextPopulationSize);
        }

        if (_plan.Stagnation is { } stagnation)
        {
            _stagnate ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<int>, ArrayView<double>, double, int, int, ArrayView<int>>>(
                nameof(BookkeepingKernels.Stagnate));
            _stagnate(_stream, 1, views.CurrentFitness, Views.BestIndex, _lastBest, stagnation.Threshold, stagnation.MaxStreak, generation, Views.Stop);
        }
    }

    /// <summary>Frees the buffers and the kernels.</summary>
    public void Dispose()
    {
        foreach (var kernel in _kernels)
        {
            kernel.Dispose();
        }

        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }

        _kernels.Clear();
        _buffers.Clear();
    }

    /// <summary>The length the bitonic network sorts for <paramref name="populationSize"/>: the next power of two.</summary>
    /// <param name="populationSize">N.</param>
    /// <returns>The length; N itself when ranking by counting.</returns>
    internal static int SortLength(int populationSize) =>
        populationSize <= CountingRankLimit ? populationSize : (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)populationSize);

    /// <summary>Ranks the first <paramref name="count"/> fitness values into the ranking.</summary>
    /// <param name="fitness">The fitness values.</param>
    /// <param name="count">N.</param>
    internal void Rank(ArrayView<double> fitness, int count)
    {
        if (count <= CountingRankLimit)
        {
            _rankByCounting ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>>>(
                nameof(BookkeepingKernels.RankByCounting));
            _rankByCounting(_stream, count, fitness, count, Views.Ranking, Views.Stop);
            return;
        }

        _loadSortKeys ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<double>, ArrayView<int>, ArrayView<int>>>(
            nameof(BookkeepingKernels.LoadSortKeys));
        _bitonicStep ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<int>, int, int, ArrayView<int>>>(
            nameof(BookkeepingKernels.BitonicStep));
        var length = SortLength(count);
        _loadSortKeys(_stream, length, fitness, count, _sortKeys, Views.Ranking, Views.Stop);
        for (var block = 2; block <= length; block <<= 1)
        {
            for (var span = block >> 1; span > 0; span >>= 1)
            {
                _bitonicStep(_stream, length, _sortKeys, Views.Ranking, span, block, Views.Stop);
            }
        }
    }

    /// <summary>Finds the best of the first <paramref name="count"/> fitness values into the best index.</summary>
    /// <param name="fitness">The fitness values.</param>
    /// <param name="count">N.</param>
    internal void FindBest(ArrayView<double> fitness, int count)
    {
        _bestOfChunks ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>>>(
            nameof(BookkeepingKernels.BestOfChunks));
        _bestOfPartials ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>, ArrayView<int>>>(
            nameof(BookkeepingKernels.BestOfPartials));
        var chunks = BookkeepingKernels.ChunkCount(count);
        _bestOfChunks(_stream, chunks, fitness, count, _partialIndices, Views.Stop);
        _bestOfPartials(_stream, 1, fitness, chunks, _partialIndices, Views.BestIndex, Views.Stop);
    }

    private void UpdateArchive(ArrayView<double> parents, int generation, int count, int capacity)
    {
        _countImproved ??= Load<Action<AcceleratorStream, Index1D, ArrayView<int>, int, ArrayView<int>, ArrayView<int>>>(
            nameof(BookkeepingKernels.CountImproved));
        _scanImproved ??= Load<Action<AcceleratorStream, Index1D, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>>>(
            nameof(BookkeepingKernels.ScanImproved));
        _placeImproved ??= Load<Action<AcceleratorStream, Index1D, ArrayView<int>, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>, int, int, ArrayView<int>>>(
            nameof(BookkeepingKernels.PlaceImproved));
        _copyToArchive ??= Load<Action<AcceleratorStream, Index1D, ArrayView<int>, ArrayView<double>, ArrayView<double>, int, ArrayView<int>>>(
            nameof(BookkeepingKernels.CopyToArchive));
        var chunks = BookkeepingKernels.ChunkCount(count);
        _countImproved(_stream, chunks, Views.Outcomes, count, _counts, Views.Stop);
        _scanImproved(_stream, 1, chunks, _counts, Views.ArchiveSize, capacity, Views.Stop);
        _placeImproved(_stream, chunks, Views.Outcomes, count, _counts, Views.ArchiveSize, capacity, _owners, _seed, generation, Views.Stop);
        _copyToArchive(_stream, capacity, _owners, parents, Views.Archive, _plan.GenomeSize, Views.Stop);
    }

    private void Adapt(PopulationViews views, int count)
    {
        _sumSuccesses ??= Load<Action<AcceleratorStream, Index1D, ParameterRule, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>>>(
            nameof(BookkeepingKernels.SumSuccesses));
        _adapt ??= Load<Action<AcceleratorStream, Index1D, ParameterRule, int, ArrayView<double>, double, int, MemoryRule, ArrayView<double>, ArrayView<int>, ArrayView<int>>>(
            nameof(BookkeepingKernels.Adapt));
        var chunks = BookkeepingKernels.ChunkCount(count);
        _sumSuccesses(_stream, chunks, _plan.Rule, count, Views, views.NextFitness, views.CurrentFitness, _partials);
        _adapt(_stream, 1, _plan.Rule, chunks, _partials, _plan.AdaptationRate, _plan.MemorySize, _plan.LShade ? MemoryRule.LShade : MemoryRule.Shade, Views.Adaptation, _memoryIndex, Views.Stop);
    }

    private void FillInts(ArrayView<int> target, int value)
    {
        _fillInts ??= Load<Action<AcceleratorStream, Index1D, ArrayView<int>, int>>(nameof(BookkeepingKernels.FillInts));
        _fillInts(_stream, target.IntLength, target, value);
    }

    private void FillDoubles(ArrayView<double> target, double value)
    {
        _fillDoubles ??= Load<Action<AcceleratorStream, Index1D, ArrayView<double>, double>>(nameof(BookkeepingKernels.FillDoubles));
        _fillDoubles(_stream, target.IntLength, target, value);
    }

    private ArrayView<int> Ints(long length)
    {
        var buffer = _accelerator.Allocate1D<int>(length);
        _buffers.Add(buffer);
        return buffer.View;
    }

    private ArrayView<double> Doubles(long length)
    {
        var buffer = _accelerator.Allocate1D<double>(length);
        _buffers.Add(buffer);
        return buffer.View;
    }

    private TDelegate Load<TDelegate>(string name)
        where TDelegate : Delegate
    {
        var method = typeof(BookkeepingKernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
        var kernel = KernelLoader.Load(_accelerator, method);
        _kernels.Add(kernel);
        return kernel.CreateLauncherDelegate<TDelegate>();
    }
}
