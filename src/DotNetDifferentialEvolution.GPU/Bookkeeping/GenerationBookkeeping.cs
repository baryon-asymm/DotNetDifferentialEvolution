using System.Diagnostics;
using System.Reflection;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The device state of one run's configuration and the passes that keep it between generations, enqueued on the
/// accelerator's default stream in the CPU package's order (BOOT.md): the ranking, the archive, the adaptation, L-SHADE's
/// reduction, the best index, the stop rule. Allocates only what the configuration uses and loads exactly the kernels it
/// uses, once, in the constructor (check A10), each through <see cref="KernelLoader"/> with the largest extent it is
/// launched with; no pass is loaded afterwards.
/// </summary>
internal sealed class GenerationBookkeeping : IDisposable
{
    /// <summary>The calls timed per ranking and size, after the warm-up: the median of them is the time.</summary>
    private const int TimedCalls = 3;

    private readonly Accelerator _accelerator;
    private readonly AcceleratorStream _stream;
    private readonly BookkeepingPlan _plan;
    private readonly int _seed;
    private readonly List<RankingMeasurement> _measurements = [];
    private readonly List<MemoryBuffer> _buffers = [];
    private readonly List<Kernel> _kernels = [];
    private readonly ArrayView<int> _partialIndices;
    private readonly ArrayView<int> _counts;
    private readonly ArrayView<int> _owners;
    private readonly ArrayView<double> _partials;
    private readonly ArrayView<double> _largestWeights;
    private readonly ArrayView<long> _sortKeys;
    private readonly ArrayView<int> _memoryIndex;
    private readonly ArrayView<double> _lastBest;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, int>? _fillInts;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, double>? _fillDoubles;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, int, ArrayView<int>, ArrayView<int>>? _bestOfChunks;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>, ArrayView<int>>? _bestOfPartials;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>>? _rankByCounting;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<long>, ArrayView<int>, ArrayView<int>>? _loadSortKeys;
    private Action<AcceleratorStream, Index1D, ArrayView<long>, ArrayView<int>, int, int, ArrayView<int>>? _bitonicStep;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, int, int, ArrayView<int>, ArrayView<int>>? _countImproved;
    private Action<AcceleratorStream, Index1D, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>>? _scanImproved;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, int, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>, int, int, ArrayView<int>>? _placeImproved;
    private Action<AcceleratorStream, Index1D, ArrayView<int>, ArrayView<double>, ArrayView<double>, int, ArrayView<int>>? _copyToArchive;
    private Action<AcceleratorStream, Index1D, ParameterRule, int, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>>? _largestWeightsPass;
    private Action<AcceleratorStream, Index1D, ParameterRule, int, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>>? _sumSuccesses;
    private Action<AcceleratorStream, Index1D, ParameterRule, int, ArrayView<double>, double, int, MemoryRule, ArrayView<double>, ArrayView<int>, ArrayView<int>>? _adapt;
    private Action<AcceleratorStream, Index1D, PopulationViews, int, StrategyViews, int>? _compact;
    private Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<int>, ArrayView<double>, double, int, int, ArrayView<int>>? _stagnate;

    /// <summary>Allocates the configuration's buffers and writes their initial state.</summary>
    /// <param name="accelerator">The accelerator, bound to the calling thread.</param>
    /// <param name="plan">What the configuration keeps.</param>
    /// <param name="seed">The run's seed, for the archive's slot draws.</param>
    public GenerationBookkeeping(Accelerator accelerator, BookkeepingPlan plan, int seed)
        : this(accelerator, plan, seed, new BookkeepingTuning())
    {
    }

    /// <summary>
    /// The same, with the ranking limit and the wide chunk forced by a test (checks A3 and A4): a forced limit times
    /// nothing, and loads follow it as they follow a limit that was timed or untimed.
    /// </summary>
    /// <param name="accelerator">The accelerator, bound to the calling thread.</param>
    /// <param name="plan">What the configuration keeps.</param>
    /// <param name="seed">The run's seed, for the archive's slot draws.</param>
    /// <param name="tuning">What is forced; a <see langword="null"/> member is decided as the package decides it.</param>
    internal GenerationBookkeeping(Accelerator accelerator, BookkeepingPlan plan, int seed, BookkeepingTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(accelerator);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentOutOfRangeException.ThrowIfLessThan(tuning.RankingLimit ?? 1, 1, nameof(tuning));
        ArgumentOutOfRangeException.ThrowIfLessThan(tuning.WideChunkSize ?? 1, 1, nameof(tuning));
        _accelerator = accelerator;
        _stream = accelerator.DefaultStream;
        _plan = plan;
        _seed = seed;
        WideChunkSize = tuning.WideChunkSize ?? BookkeepingKernels.WideChunkSizeOf(plan.PopulationSize);
        try
        {
            var populationSize = plan.PopulationSize;
            var timed = plan.NeedsRanking && tuning.RankingLimit is null && IsTimed(accelerator, populationSize);
            RankingLimit = tuning.RankingLimit ?? UntimedLimitOf(accelerator, populationSize);
            var chunks = BookkeepingKernels.ChunkCount(populationSize);
            var wideChunks = BookkeepingKernels.WideChunkCount(populationSize, WideChunkSize);
            var network = timed || populationSize > RankingLimit;
            var rankingLength = !plan.NeedsRanking ? 1 : network ? SortLength(populationSize) : populationSize;
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
            _partialIndices = Ints(plan.NeedsBestIndex ? wideChunks : 1);
            _counts = Ints(archiveCapacity > 0 ? wideChunks : 1);
            _owners = Ints(Math.Max(1, archiveCapacity));
            _partials = Doubles(plan.Adapts ? (long)chunks * SuccessSums.Width : 1);
            _largestWeights = Doubles(plan.Rule == ParameterRule.Shade ? wideChunks : 1);
            _sortKeys = Longs(plan.NeedsRanking && network ? rankingLength : 1);
            _memoryIndex = Ints(1);
            _lastBest = Doubles(1);

            LoadKernels(chunks, wideChunks, rankingLength, adaptationLength, timed);

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

            if (timed)
            {
                RankingLimit = CalibrateRankingLimit();
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
    /// Gets L, the largest N ranked by counting; above it, the bitonic network. Timed on the device in the constructor on
    /// CUDA and OpenCL for a ranking plan above <see cref="RankingCalibration.Floor"/> (check A3), else
    /// <see cref="RankingCalibration.Floor"/> or <see cref="RankingCalibration.UntimedLimit"/> (the CPU accelerator), or
    /// what a test forced.
    /// </summary>
    public int RankingLimit { get; private set; }

    /// <summary>Gets c, the individuals one thread of a wide pass walks: fixed at construction from N_init (check A4), or what a test forced.</summary>
    public int WideChunkSize { get; }

    /// <summary>Gets what the constructor's calibration measured, one entry per population size timed; empty when nothing was timed.</summary>
    internal IReadOnlyList<RankingMeasurement> RankingMeasurements => _measurements;

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
            Required(_compact)(_stream, nextPopulationSize, views, _plan.GenomeSize, Views, nextArchiveCapacity);
            views = views.Swapped();
        }

        if (_plan.NeedsBestIndex)
        {
            FindBest(views.CurrentFitness, nextPopulationSize);
        }

        if (_plan.Stagnation is { } stagnation)
        {
            Required(_stagnate)(_stream, 1, views.CurrentFitness, Views.BestIndex, _lastBest, stagnation.Threshold, stagnation.MaxStreak, generation, Views.Stop);
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
    /// <returns>The length.</returns>
    internal static int SortLength(int populationSize) =>
        (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)populationSize);

    /// <summary>Ranks the first <paramref name="count"/> fitness values into the ranking: by counting up to <see cref="RankingLimit"/>, else by the network.</summary>
    /// <param name="fitness">The fitness values.</param>
    /// <param name="count">N.</param>
    internal void Rank(ArrayView<double> fitness, int count)
    {
        if (count <= RankingLimit)
        {
            RankByCounting(fitness, count);
        }
        else
        {
            RankByBitonicNetwork(fitness, count);
        }
    }

    /// <summary>Ranks by counting, whatever N; the ranking buffer must hold N (ACCEPTANCE.md, S9).</summary>
    /// <param name="fitness">The fitness values.</param>
    /// <param name="count">N.</param>
    internal void RankByCounting(ArrayView<double> fitness, int count) =>
        Required(_rankByCounting)(_stream, count, fitness, count, Views.Ranking, Views.Stop);

    /// <summary>Ranks by the bitonic network, whatever N; the ranking and key buffers must hold N rounded up to a power of two (ACCEPTANCE.md, S9).</summary>
    /// <param name="fitness">The fitness values.</param>
    /// <param name="count">N.</param>
    internal void RankByBitonicNetwork(ArrayView<double> fitness, int count)
    {
        var loadSortKeys = Required(_loadSortKeys);
        var bitonicStep = Required(_bitonicStep);
        var length = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)count);
        loadSortKeys(_stream, length, fitness, count, _sortKeys, Views.Ranking, Views.Stop);
        for (var block = 2; block <= length; block <<= 1)
        {
            for (var span = block >> 1; span > 0; span >>= 1)
            {
                bitonicStep(_stream, length, _sortKeys, Views.Ranking, span, block, Views.Stop);
            }
        }
    }

    /// <summary>Finds the best of the first <paramref name="count"/> fitness values into the best index.</summary>
    /// <param name="fitness">The fitness values.</param>
    /// <param name="count">N.</param>
    internal void FindBest(ArrayView<double> fitness, int count)
    {
        var bestOfChunks = Required(_bestOfChunks);
        var bestOfPartials = Required(_bestOfPartials);
        var chunks = BookkeepingKernels.WideChunkCount(count, WideChunkSize);
        bestOfChunks(_stream, chunks, fitness, count, WideChunkSize, _partialIndices, Views.Stop);
        bestOfPartials(_stream, 1, fitness, chunks, _partialIndices, Views.BestIndex, Views.Stop);
    }

    private void UpdateArchive(ArrayView<double> parents, int generation, int count, int capacity)
    {
        var chunks = BookkeepingKernels.WideChunkCount(count, WideChunkSize);
        Required(_countImproved)(_stream, chunks, Views.Outcomes, count, WideChunkSize, _counts, Views.Stop);
        Required(_scanImproved)(_stream, 1, chunks, _counts, Views.ArchiveSize, capacity, Views.Stop);
        Required(_placeImproved)(_stream, chunks, Views.Outcomes, count, WideChunkSize, _counts, Views.ArchiveSize, capacity, _owners, _seed, generation, Views.Stop);
        Required(_copyToArchive)(_stream, capacity, _owners, parents, Views.Archive, _plan.GenomeSize, Views.Stop);
    }

    private void Adapt(PopulationViews views, int count)
    {
        var chunks = BookkeepingKernels.ChunkCount(count);
        if (_plan.Rule == ParameterRule.Shade)
        {
            Required(_largestWeightsPass)(_stream, BookkeepingKernels.WideChunkCount(count, WideChunkSize), _plan.Rule, count, WideChunkSize, Views, views.NextFitness, views.CurrentFitness, _largestWeights);
        }

        Required(_sumSuccesses)(_stream, chunks, _plan.Rule, count, WideChunkSize, Views, views.NextFitness, views.CurrentFitness, _largestWeights, _partials);
        Required(_adapt)(_stream, 1, _plan.Rule, chunks, _partials, _plan.AdaptationRate, _plan.MemorySize, _plan.LShade ? MemoryRule.LShade : MemoryRule.Shade, Views.Adaptation, _memoryIndex, Views.Stop);
    }

    /// <summary>Whether the ranking limit of a population of <paramref name="populationSize"/> is timed: on CUDA and OpenCL, above the floor (check A3).</summary>
    private static bool IsTimed(Accelerator accelerator, int populationSize) =>
        accelerator.AcceleratorType is AcceleratorType.Cuda or AcceleratorType.OpenCL && populationSize > RankingCalibration.Floor;

    /// <summary>The limit without timing: the floor for a population that is never timed on a device, else <see cref="RankingCalibration.UntimedLimit"/>.</summary>
    private static int UntimedLimitOf(Accelerator accelerator, int populationSize) =>
        accelerator.AcceleratorType is AcceleratorType.Cuda or AcceleratorType.OpenCL && populationSize <= RankingCalibration.Floor
            ? RankingCalibration.Floor
            : RankingCalibration.UntimedLimit;

    /// <summary>
    /// Times both rankings over a scratch buffer of N_init zeros, released after, and asks <see cref="RankingCalibration"/>
    /// for the limit. Needs the stop word cleared and both rankings loaded.
    /// </summary>
    private int CalibrateRankingLimit()
    {
        using var scratch = _accelerator.Allocate1D<double>(_plan.PopulationSize);
        scratch.MemSetToZero();
        return RankingCalibration.LimitOf(_plan.PopulationSize, count => TimeRankings(scratch.View, count));
    }

    private RankingTimes TimeRankings(ArrayView<double> scratch, int count)
    {
        var counting = MedianMicroseconds(() => RankByCounting(scratch, count));
        var bitonic = MedianMicroseconds(() => RankByBitonicNetwork(scratch, count));
        var times = new RankingTimes(counting, bitonic);
        _measurements.Add(new RankingMeasurement(count, times));
        return times;
    }

    /// <summary>The median, in microseconds, of the timed calls of <paramref name="call"/> after one warm-up call, each followed by a synchronisation.</summary>
    private double MedianMicroseconds(Action call)
    {
        call();
        _accelerator.Synchronize();
        var times = new double[TimedCalls];
        for (var timed = 0; timed < TimedCalls; timed++)
        {
            var start = Stopwatch.GetTimestamp();
            call();
            _accelerator.Synchronize();
            times[timed] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
        }

        Array.Sort(times);
        return times[TimedCalls / 2];
    }

    private void FillInts(ArrayView<int> target, int value) =>
        Required(_fillInts)(_stream, target.IntLength, target, value);

    private void FillDoubles(ArrayView<double> target, double value) =>
        Required(_fillDoubles)(_stream, target.IntLength, target, value);

    private ArrayView<int> Ints(long length)
    {
        var buffer = _accelerator.Allocate1D<int>(length);
        _buffers.Add(buffer);
        return buffer.View;
    }

    private ArrayView<long> Longs(long length)
    {
        var buffer = _accelerator.Allocate1D<long>(length);
        _buffers.Add(buffer);
        return buffer.View;
    }

    private ArrayView<double> Doubles(long length)
    {
        var buffer = _accelerator.Allocate1D<double>(length);
        _buffers.Add(buffer);
        return buffer.View;
    }

    private static TDelegate Required<TDelegate>(TDelegate? pass)
        where TDelegate : Delegate =>
        pass ?? throw new InvalidOperationException("The plan of this bookkeeping does not use that pass, so its kernel was not loaded.");

    /// <summary>
    /// Loads, once, every kernel the plan uses, each for the largest extent it is launched with in the run: the fills for
    /// the longest buffer they fill, the ranking for N (counting: at most the ranking limit, or N when the limit is timed
    /// after the loads), the chunked passes for the chunk count of the initial population (the sums' chunks of 1 024, the
    /// other passes' wide chunks), the single-thread passes for 1, the archive copy for the capacity, the reduction for the
    /// largest next population. L-SHADE's population shrinks, so its ranking may fall below the ranking limit from above it
    /// and loads both rankings there; a limit that is timed loads both rankings, whatever it comes out.
    /// </summary>
    /// <param name="chunks">The chunk count of the initial population, for the sums.</param>
    /// <param name="wideChunks">The wide chunk count of the initial population, for the order-independent passes.</param>
    /// <param name="rankingLength">The length the ranking sorts, or 1.</param>
    /// <param name="adaptationLength">The length of the adaptation buffer.</param>
    /// <param name="timed">Whether the ranking limit is timed once the kernels are loaded.</param>
    private void LoadKernels(int chunks, int wideChunks, int rankingLength, int adaptationLength, bool timed)
    {
        var plan = _plan;
        var populationSize = plan.PopulationSize;
        var archiveCapacity = Math.Max(plan.ArchiveCapacity, 0);
        _fillInts = Load<Action<AcceleratorStream, Index1D, ArrayView<int>, int>>(
            Math.Max(BookkeepingKernels.StopLength, archiveCapacity), nameof(BookkeepingKernels.FillInts));
        _fillDoubles = Load<Action<AcceleratorStream, Index1D, ArrayView<double>, double>>(
            Math.Max(adaptationLength, plan.Rule == ParameterRule.Jde ? populationSize : 1), nameof(BookkeepingKernels.FillDoubles));

        if (plan.NeedsRanking)
        {
            if (timed || populationSize <= RankingLimit || plan.LShade)
            {
                _rankByCounting = Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>>>(
                    timed ? populationSize : Math.Min(populationSize, RankingLimit), nameof(BookkeepingKernels.RankByCounting));
            }

            if (timed || populationSize > RankingLimit)
            {
                _loadSortKeys = Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<long>, ArrayView<int>, ArrayView<int>>>(
                    rankingLength, nameof(BookkeepingKernels.LoadSortKeys));
                _bitonicStep = Load<Action<AcceleratorStream, Index1D, ArrayView<long>, ArrayView<int>, int, int, ArrayView<int>>>(
                    rankingLength, nameof(BookkeepingKernels.BitonicStep));
            }
        }

        if (plan.NeedsBestIndex)
        {
            _bestOfChunks = Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, int, ArrayView<int>, ArrayView<int>>>(
                wideChunks, nameof(BookkeepingKernels.BestOfChunks));
            _bestOfPartials = Load<Action<AcceleratorStream, Index1D, ArrayView<double>, int, ArrayView<int>, ArrayView<int>, ArrayView<int>>>(
                1, nameof(BookkeepingKernels.BestOfPartials));
        }

        if (archiveCapacity > 0)
        {
            _countImproved = Load<Action<AcceleratorStream, Index1D, ArrayView<int>, int, int, ArrayView<int>, ArrayView<int>>>(
                wideChunks, nameof(BookkeepingKernels.CountImproved));
            _scanImproved = Load<Action<AcceleratorStream, Index1D, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>>>(
                1, nameof(BookkeepingKernels.ScanImproved));
            _placeImproved = Load<Action<AcceleratorStream, Index1D, ArrayView<int>, int, int, ArrayView<int>, ArrayView<int>, int, ArrayView<int>, int, int, ArrayView<int>>>(
                wideChunks, nameof(BookkeepingKernels.PlaceImproved));
            _copyToArchive = Load<Action<AcceleratorStream, Index1D, ArrayView<int>, ArrayView<double>, ArrayView<double>, int, ArrayView<int>>>(
                archiveCapacity, nameof(BookkeepingKernels.CopyToArchive));
        }

        if (plan.Adapts)
        {
            _sumSuccesses = Load<Action<AcceleratorStream, Index1D, ParameterRule, int, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>>>(
                chunks, nameof(BookkeepingKernels.SumSuccesses));
            _adapt = Load<Action<AcceleratorStream, Index1D, ParameterRule, int, ArrayView<double>, double, int, MemoryRule, ArrayView<double>, ArrayView<int>, ArrayView<int>>>(
                1, nameof(BookkeepingKernels.Adapt));
            if (plan.Rule == ParameterRule.Shade)
            {
                _largestWeightsPass = Load<Action<AcceleratorStream, Index1D, ParameterRule, int, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>>>(
                    wideChunks, nameof(BookkeepingKernels.LargestWeights));
            }
        }

        if (plan.LShade)
        {
            _compact = Load<Action<AcceleratorStream, Index1D, PopulationViews, int, StrategyViews, int>>(
                Math.Max(1, populationSize - 1), nameof(BookkeepingKernels.Compact));
        }

        if (plan.Stagnation is not null)
        {
            _stagnate = Load<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<int>, ArrayView<double>, double, int, int, ArrayView<int>>>(
                1, nameof(BookkeepingKernels.Stagnate));
        }
    }

    private TDelegate Load<TDelegate>(int extent, string name)
        where TDelegate : Delegate
    {
        var method = typeof(BookkeepingKernels).GetMethod(name, BindingFlags.Public | BindingFlags.Static)!;
        var kernel = KernelLoader.Load(_accelerator, method, extent);
        _kernels.Add(kernel);
        return kernel.CreateLauncherDelegate<TDelegate>();
    }
}
