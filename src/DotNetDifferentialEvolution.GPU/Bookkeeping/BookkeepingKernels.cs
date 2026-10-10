using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// The passes between two generations, one kernel entry point each. A chunk pass runs one thread per chunk of
/// individuals in index order, <see cref="ChunkSize"/> for the sums, whose floating-point order is fixed, and
/// <see cref="WideChunkSize"/> for the passes whose result cannot depend on the chunking; a closing pass runs one thread
/// over the chunks in chunk order. So every result is fixed by the data alone, whatever the device's scheduling, and no
/// floating-point value is ever combined by an atomic. Every pass returns at once when the stop word is set (BOOT.md).
/// </summary>
internal static class BookkeepingKernels
{
    /// <summary>The individuals one thread of a summing pass walks, in index order: S7's order.</summary>
    public const int ChunkSize = 1024;

    /// <summary>
    /// The individuals one thread of a pass walks whose result cannot depend on the chunking (the best index, the improved
    /// count, the archive placement, the largest weight): few, so that many threads share the work (check A4).
    /// </summary>
    public const int WideChunkSize = 32;

    /// <summary>The stop word's entries: set, the generation that set it, the streak.</summary>
    public const int StopSet = 0;

    /// <summary>The entry of the stop word holding the generation the rule fired in.</summary>
    public const int StopGeneration = 1;

    /// <summary>The entry of the stop word holding the stagnation streak.</summary>
    public const int StopStreak = 2;

    /// <summary>The number of entries of the stop word.</summary>
    public const int StopLength = 3;

    /// <summary>The number of chunks of <paramref name="count"/> individuals.</summary>
    /// <param name="count">N.</param>
    /// <returns>⌈N / <see cref="ChunkSize"/>⌉.</returns>
    public static int ChunkCount(int count) => (count + ChunkSize - 1) / ChunkSize;

    /// <summary>The number of wide chunks of <paramref name="count"/> individuals.</summary>
    /// <param name="count">N.</param>
    /// <returns>⌈N / <see cref="WideChunkSize"/>⌉.</returns>
    public static int WideChunkCount(int count) => (count + WideChunkSize - 1) / WideChunkSize;

    /// <summary>Writes <paramref name="value"/> into every entry: a buffer's initial state.</summary>
    /// <param name="index">The entry.</param>
    /// <param name="target">The buffer.</param>
    /// <param name="value">The value.</param>
    public static void FillInts(Index1D index, ArrayView<int> target, int value) => target[index] = value;

    /// <summary>Writes <paramref name="value"/> into every entry: a buffer's initial state.</summary>
    /// <param name="index">The entry.</param>
    /// <param name="target">The buffer.</param>
    /// <param name="value">The value.</param>
    public static void FillDoubles(Index1D index, ArrayView<double> target, double value) => target[index] = value;

    /// <summary>
    /// The best individual of one wide chunk: the first whose fitness no later one beats (<see cref="Selection.IsBetter"/>,
    /// <see cref="double.NaN"/> worst), as <c>BestPick</c> scans, the incumbent's fitness kept in a register.
    /// </summary>
    /// <param name="chunk">The wide chunk.</param>
    /// <param name="fitness">The current fitness values.</param>
    /// <param name="count">N.</param>
    /// <param name="partialIndices">Receives the chunk's best index.</param>
    /// <param name="stop">The stop word.</param>
    public static void BestOfChunks(Index1D chunk, ArrayView<double> fitness, int count, ArrayView<int> partialIndices, ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        var first = chunk * WideChunkSize;
        var end = Math.Min(first + WideChunkSize, count);
        var best = first;
        var bestFitness = fitness[first];
        for (var i = first + 1; i < end; i++)
        {
            var candidate = fitness[i];
            if (Selection.IsBetter(candidate, bestFitness))
            {
                best = i;
                bestFitness = candidate;
            }
        }

        partialIndices[chunk] = best;
    }

    /// <summary>The best of the wide chunks' best, in chunk order, into the best index. One thread.</summary>
    /// <param name="index">The thread; only thread 0 works.</param>
    /// <param name="fitness">The current fitness values.</param>
    /// <param name="chunks">The number of wide chunks.</param>
    /// <param name="partialIndices">The wide chunks' best indices.</param>
    /// <param name="bestIndex">Receives the best index.</param>
    /// <param name="stop">The stop word.</param>
    public static void BestOfPartials(Index1D index, ArrayView<double> fitness, int chunks, ArrayView<int> partialIndices, ArrayView<int> bestIndex, ArrayView<int> stop)
    {
        if (index != 0 || stop[StopSet] != 0)
        {
            return;
        }

        var best = partialIndices[0];
        var bestFitness = fitness[best];
        for (var c = 1; c < chunks; c++)
        {
            var candidate = partialIndices[c];
            var candidateFitness = fitness[candidate];
            if (Selection.IsBetter(candidateFitness, bestFitness))
            {
                best = candidate;
                bestFitness = candidateFitness;
            }
        }

        bestIndex[0] = best;
    }

    /// <summary>
    /// The ranking by counting: individual i's rank is the number of individuals that precede it
    /// (<see cref="FitnessOrder.Precedes"/> on <see cref="FitnessOrder.OrderKey"/>), and i is written at that rank. One
    /// launch, N threads, N reads each.
    /// </summary>
    /// <param name="index">The individual.</param>
    /// <param name="fitness">The current fitness values.</param>
    /// <param name="count">N.</param>
    /// <param name="ranking">Receives the ranking, best first.</param>
    /// <param name="stop">The stop word.</param>
    public static void RankByCounting(Index1D index, ArrayView<double> fitness, int count, ArrayView<int> ranking, ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        int individual = index;
        var key = FitnessOrder.OrderKey(fitness[individual]);
        var rank = 0;
        for (var other = 0; other < count; other++)
        {
            if (FitnessOrder.Precedes(FitnessOrder.OrderKey(fitness[other]), other, key, individual))
            {
                rank++;
            }
        }

        ranking[rank] = individual;
    }

    /// <summary>
    /// The bitonic network's input: entry t holds (order key, t) for t &lt; N, and (the key of +∞, t) for the padding up
    /// to a power of two, which therefore sorts after every individual.
    /// </summary>
    /// <param name="index">The entry.</param>
    /// <param name="fitness">The current fitness values.</param>
    /// <param name="count">N.</param>
    /// <param name="keys">Receives the order keys, a power of two long.</param>
    /// <param name="ranking">Receives the indices, as long as the keys.</param>
    /// <param name="stop">The stop word.</param>
    public static void LoadSortKeys(Index1D index, ArrayView<double> fitness, int count, ArrayView<long> keys, ArrayView<int> ranking, ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        int entry = index;
        keys[entry] = entry < count ? FitnessOrder.OrderKey(fitness[entry]) : FitnessOrder.InfinityKey;
        ranking[entry] = entry;
    }

    /// <summary>
    /// One compare-exchange step of the bitonic network over global memory: entry t and entry <c>t ⊕ span</c> are put
    /// in ascending order when <c>t &amp; block</c> is 0 and in descending order otherwise.
    /// </summary>
    /// <param name="index">The entry t.</param>
    /// <param name="keys">The order keys.</param>
    /// <param name="ranking">The indices, moved with their keys.</param>
    /// <param name="span">The distance between the two entries compared.</param>
    /// <param name="block">The size of the bitonic sequences being merged.</param>
    /// <param name="stop">The stop word.</param>
    public static void BitonicStep(Index1D index, ArrayView<long> keys, ArrayView<int> ranking, int span, int block, ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        int entry = index;
        var partner = entry ^ span;
        if (partner <= entry)
        {
            return;
        }

        var keyA = keys[entry];
        var indexA = ranking[entry];
        var keyB = keys[partner];
        var indexB = ranking[partner];
        var ascending = (entry & block) == 0;
        var swap = ascending
            ? FitnessOrder.Precedes(keyB, indexB, keyA, indexA)
            : FitnessOrder.Precedes(keyA, indexA, keyB, indexB);
        if (swap)
        {
            keys[entry] = keyB;
            ranking[entry] = indexB;
            keys[partner] = keyA;
            ranking[partner] = indexA;
        }
    }

    /// <summary>The number of improved trials of one wide chunk.</summary>
    /// <param name="chunk">The wide chunk.</param>
    /// <param name="outcomes">The trials' outcomes.</param>
    /// <param name="count">N.</param>
    /// <param name="counts">Receives the wide chunk's count.</param>
    /// <param name="stop">The stop word.</param>
    public static void CountImproved(Index1D chunk, ArrayView<int> outcomes, int count, ArrayView<int> counts, ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        var first = chunk * WideChunkSize;
        var end = Math.Min(first + WideChunkSize, count);
        var improved = 0;
        for (var i = first; i < end; i++)
        {
            if (outcomes[i] == Selection.Improved)
            {
                improved++;
            }
        }

        counts[chunk] = improved;
    }

    /// <summary>
    /// The archive's scan, one thread: each wide chunk's count becomes the number of improved trials before the chunk; the
    /// size before the generation, cut to the capacity as the CPU loop cuts it, goes to entry 1 of the size; the new size
    /// is that plus every improved trial, at most the capacity.
    /// </summary>
    /// <param name="index">The thread; only thread 0 works.</param>
    /// <param name="chunks">The number of wide chunks.</param>
    /// <param name="counts">The wide chunks' counts; receives the offsets.</param>
    /// <param name="archiveSize">Entry 0: the size; entry 1 receives the size before.</param>
    /// <param name="capacity">The capacity; at least 1.</param>
    /// <param name="stop">The stop word.</param>
    public static void ScanImproved(Index1D index, int chunks, ArrayView<int> counts, ArrayView<int> archiveSize, int capacity, ArrayView<int> stop)
    {
        if (index != 0 || stop[StopSet] != 0)
        {
            return;
        }

        var before = Math.Min(archiveSize[0], capacity);
        var running = 0;
        for (var c = 0; c < chunks; c++)
        {
            var chunkCount = counts[c];
            counts[c] = running;
            running += chunkCount;
        }

        archiveSize[1] = before;
        archiveSize[0] = Math.Min(capacity, before + running);
    }

    /// <summary>
    /// The archive slots of one wide chunk's improved parents (<see cref="ArchiveRules.SlotOf"/>), each slot claimed by the
    /// highest index drawn to it (<see cref="Atomic.Max(ref int, int)"/>).
    /// </summary>
    /// <param name="chunk">The wide chunk.</param>
    /// <param name="outcomes">The trials' outcomes.</param>
    /// <param name="count">N.</param>
    /// <param name="offsets">The number of improved trials before each wide chunk.</param>
    /// <param name="archiveSize">Entry 1: the size before the generation.</param>
    /// <param name="capacity">The capacity; at least 1.</param>
    /// <param name="owners">Each slot's claimant, −1 for none.</param>
    /// <param name="seed">The run's seed.</param>
    /// <param name="generation">The generation just run.</param>
    /// <param name="stop">The stop word.</param>
    public static void PlaceImproved(
        Index1D chunk,
        ArrayView<int> outcomes,
        int count,
        ArrayView<int> offsets,
        ArrayView<int> archiveSize,
        int capacity,
        ArrayView<int> owners,
        int seed,
        int generation,
        ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        var first = chunk * WideChunkSize;
        var end = Math.Min(first + WideChunkSize, count);
        var fillPosition = archiveSize[1] + offsets[chunk];
        for (var i = first; i < end; i++)
        {
            if (outcomes[i] != Selection.Improved)
            {
                continue;
            }

            var draws = new PhiloxDraws(seed, i, generation, PhiloxDraws.ArchiveStream);
            var slot = ArchiveRules.SlotOf(ref draws, fillPosition, capacity);
            _ = Atomic.Max(ref owners[slot], i);
            fillPosition++;
        }
    }

    /// <summary>Copies a claimed slot's parent into the archive and frees the claim.</summary>
    /// <param name="index">The slot.</param>
    /// <param name="owners">Each slot's claimant, −1 for none; reset to −1.</param>
    /// <param name="parents">The population before the generation, the discarded parents.</param>
    /// <param name="archive">The archive.</param>
    /// <param name="genomeSize">D.</param>
    /// <param name="stop">The stop word.</param>
    public static void CopyToArchive(Index1D index, ArrayView<int> owners, ArrayView<double> parents, ArrayView<double> archive, int genomeSize, ArrayView<int> stop)
    {
        if (stop[StopSet] != 0)
        {
            return;
        }

        int slot = index;
        var owner = owners[slot];
        if (owner < 0)
        {
            return;
        }

        for (var j = 0; j < genomeSize; j++)
        {
            archive[slot * genomeSize + j] = parents[owner * genomeSize + j];
        }

        owners[slot] = -1;
    }

    /// <summary>
    /// The largest SHADE weight (<see cref="AdaptationRules.WeightOf"/>) of one wide chunk's improved trials, 0 when it has none:
    /// the first pass of the scale that keeps the sums finite (<see cref="AdaptationRules.ScaleOf"/>). Runs under SHADE and
    /// L-SHADE only.
    /// </summary>
    /// <param name="chunk">The wide chunk.</param>
    /// <param name="rule">SHADE.</param>
    /// <param name="count">N.</param>
    /// <param name="strategy">The trials' outcomes.</param>
    /// <param name="parentFitness">The fitness before the generation.</param>
    /// <param name="trialFitness">The fitness after it, the trial's where it improved.</param>
    /// <param name="largest">Receives the wide chunk's largest weight, one double per wide chunk.</param>
    public static void LargestWeights(
        Index1D chunk,
        ParameterRule rule,
        int count,
        StrategyViews strategy,
        ArrayView<double> parentFitness,
        ArrayView<double> trialFitness,
        ArrayView<double> largest)
    {
        if (strategy.Stop[StopSet] != 0)
        {
            return;
        }

        var first = chunk * WideChunkSize;
        var end = Math.Min(first + WideChunkSize, count);
        var maximum = 0.0;
        for (var i = first; i < end; i++)
        {
            var weight = AdaptationRules.WeightOf(rule, strategy.Outcomes[i], parentFitness[i], trialFitness[i]);
            if (weight > maximum)
            {
                maximum = weight;
            }
        }

        largest[chunk] = maximum;
    }

    /// <summary>
    /// The sums of one chunk's improved trials (<see cref="AdaptationRules.WeightOf"/> over the generation's scale,
    /// <see cref="SuccessSums.Add"/>), in index order. Under SHADE the scale is <see cref="AdaptationRules.ScaleOf"/> of the
    /// largest of the wide chunks' largest weights, and the division is skipped when it is 1; under JADE it is 1.
    /// </summary>
    /// <param name="chunk">The chunk.</param>
    /// <param name="rule">JADE or SHADE.</param>
    /// <param name="count">N.</param>
    /// <param name="strategy">The trials' F, CR and outcomes.</param>
    /// <param name="parentFitness">The fitness before the generation.</param>
    /// <param name="trialFitness">The fitness after it, the trial's where it improved.</param>
    /// <param name="largest">The wide chunks' largest weights (<see cref="LargestWeights"/>); read under SHADE only.</param>
    /// <param name="partials">Receives the chunk's sums, <see cref="SuccessSums.Width"/> doubles per chunk.</param>
    public static void SumSuccesses(
        Index1D chunk,
        ParameterRule rule,
        int count,
        StrategyViews strategy,
        ArrayView<double> parentFitness,
        ArrayView<double> trialFitness,
        ArrayView<double> largest,
        ArrayView<double> partials)
    {
        if (strategy.Stop[StopSet] != 0)
        {
            return;
        }

        var scale = 1.0;
        if (rule == ParameterRule.Shade)
        {
            var maximum = 0.0;
            for (var c = 0; c < WideChunkCount(count); c++)
            {
                if (largest[c] > maximum)
                {
                    maximum = largest[c];
                }
            }

            scale = AdaptationRules.ScaleOf(maximum, count);
        }

        var first = chunk * ChunkSize;
        var end = Math.Min(first + ChunkSize, count);
        var sums = default(SuccessSums);
        for (var i = first; i < end; i++)
        {
            var weight = AdaptationRules.WeightOf(rule, strategy.Outcomes[i], parentFitness[i], trialFitness[i]);
            if (!double.IsNaN(weight))
            {
                // Division by 1.0 changes no bit, and a double division is slow on the device: skipped when the scale is 1.
                var scaled = scale == 1.0 ? weight : weight / scale;
                sums = sums.Add(scaled, strategy.CrossoverProbabilities[i], strategy.MutationForces[i]);
            }
        }

        Store(partials, chunk, sums);
    }

    /// <summary>
    /// The adaptation, one thread: the chunks' sums added in chunk order, then JADE's means or SHADE's slot at the memory
    /// index updated (<see cref="AdaptationRules"/>), the index advanced when it was.
    /// </summary>
    /// <param name="index">The thread; only thread 0 works.</param>
    /// <param name="rule">JADE or SHADE.</param>
    /// <param name="chunks">The number of chunks.</param>
    /// <param name="partials">The chunks' sums.</param>
    /// <param name="adaptationRate">JADE's c.</param>
    /// <param name="memorySize">SHADE's H.</param>
    /// <param name="memoryRule">SHADE's or L-SHADE's update of a memory slot.</param>
    /// <param name="adaptation">JADE's μCR and μF, or SHADE's memory.</param>
    /// <param name="memoryIndex">Entry 0: SHADE's memory index.</param>
    /// <param name="stop">The stop word.</param>
    public static void Adapt(
        Index1D index,
        ParameterRule rule,
        int chunks,
        ArrayView<double> partials,
        double adaptationRate,
        int memorySize,
        MemoryRule memoryRule,
        ArrayView<double> adaptation,
        ArrayView<int> memoryIndex,
        ArrayView<int> stop)
    {
        if (index != 0 || stop[StopSet] != 0)
        {
            return;
        }

        var sums = default(SuccessSums);
        for (var c = 0; c < chunks; c++)
        {
            sums = sums.Combine(Load(partials, c));
        }

        if (rule == ParameterRule.Jade)
        {
            var meanCr = adaptation[0];
            var meanF = adaptation[1];
            AdaptationRules.UpdateJadeMeans(sums, adaptationRate, ref meanCr, ref meanF);
            adaptation[0] = meanCr;
            adaptation[1] = meanF;
            return;
        }

        var slot = memoryIndex[0];
        var slotCr = adaptation[slot];
        var slotF = adaptation[memorySize + slot];
        if (AdaptationRules.UpdateShadeSlot(sums, memoryRule == MemoryRule.LShade, ref slotCr, ref slotF))
        {
            adaptation[slot] = slotCr;
            adaptation[memorySize + slot] = slotF;
            memoryIndex[0] = (slot + 1) % memorySize;
        }
    }

    /// <summary>
    /// L-SHADE's reduction: survivor k is the individual at rank k, copied to slot k of the other buffers; the ranking
    /// becomes the identity; thread 0 cuts the archive's size to the new capacity.
    /// </summary>
    /// <param name="index">The survivor k.</param>
    /// <param name="views">The population; Current is read, Next written, and the caller swaps them.</param>
    /// <param name="genomeSize">D.</param>
    /// <param name="strategy">The ranking and the archive size.</param>
    /// <param name="archiveCapacity">The archive's capacity for the new size.</param>
    public static void Compact(Index1D index, PopulationViews views, int genomeSize, StrategyViews strategy, int archiveCapacity)
    {
        if (strategy.Stop[StopSet] != 0)
        {
            return;
        }

        int survivor = index;
        var source = strategy.Ranking[survivor];
        for (var j = 0; j < genomeSize; j++)
        {
            views.Next[survivor * genomeSize + j] = views.Current[source * genomeSize + j];
        }

        views.NextFitness[survivor] = views.CurrentFitness[source];
        strategy.Ranking[survivor] = survivor;
        if (survivor == 0)
        {
            strategy.ArchiveSize[0] = Math.Min(strategy.ArchiveSize[0], archiveCapacity);
        }
    }

    /// <summary>
    /// The stagnation rule after a generation, one thread (<see cref="StagnationRule.Apply"/>) on the best individual's
    /// fitness; when it fires, the stop word is set with the generation.
    /// </summary>
    /// <param name="index">The thread; only thread 0 works.</param>
    /// <param name="fitness">The current fitness values.</param>
    /// <param name="bestIndex">The best index.</param>
    /// <param name="lastBest">Entry 0: the last best value that counted as progress.</param>
    /// <param name="threshold">The stagnation threshold.</param>
    /// <param name="maxStagnationStreak">The streak at which the run stops.</param>
    /// <param name="generation">The generation just run.</param>
    /// <param name="stop">The stop word, with the streak.</param>
    public static void Stagnate(
        Index1D index,
        ArrayView<double> fitness,
        ArrayView<int> bestIndex,
        ArrayView<double> lastBest,
        double threshold,
        int maxStagnationStreak,
        int generation,
        ArrayView<int> stop)
    {
        if (index != 0 || stop[StopSet] != 0)
        {
            return;
        }

        var last = lastBest[0];
        var streak = stop[StopStreak];
        var stops = StagnationRule.Apply(fitness[bestIndex[0]], threshold, maxStagnationStreak, ref last, ref streak);
        lastBest[0] = last;
        stop[StopStreak] = streak;
        if (stops)
        {
            stop[StopGeneration] = generation;
            stop[StopSet] = 1;
        }
    }

    private static void Store(ArrayView<double> partials, int chunk, SuccessSums sums)
    {
        var offset = chunk * SuccessSums.Width;
        partials[offset] = sums.Weight;
        partials[offset + 1] = sums.WeightedCr;
        partials[offset + 2] = sums.WeightedCrSquared;
        partials[offset + 3] = sums.WeightedF;
        partials[offset + 4] = sums.WeightedFSquared;
        partials[offset + 5] = sums.MaxCr;
    }

    private static SuccessSums Load(ArrayView<double> partials, int chunk)
    {
        var offset = chunk * SuccessSums.Width;
        return new SuccessSums(
            partials[offset],
            partials[offset + 1],
            partials[offset + 2],
            partials[offset + 3],
            partials[offset + 4],
            partials[offset + 5]);
    }
}
