# API.md — DotNetDifferentialEvolution.GPU/Bookkeeping

Namespace: `DotNetDifferentialEvolution.GPU.Bookkeeping`. Internal to the assembly;
visible to the GPU test project and to ILGPU's runtime assembly. Consumed by the
package root.

## Internal to the assembly ✅

Built 2026-10-05 (checks S7–S12 of this node's [ACCEPTANCE.md](ACCEPTANCE.md); S6 and S14
of the package's [ACCEPTANCE.md](../ACCEPTANCE.md)); ranking by integer keys built 2026-10-10
(check A3); the ranking limit calibrated per device and the wide chunk `⌈√N⌉` built 2026-10-10
(checks A3 ⚠ and A4 ⚠, [HISTORY.md](../HISTORY.md#ranking-calibrated-2026-10-10)).

```csharp
internal static class FitnessOrder           // the one total order (S9, S10, A3)
{
    public const long InfinityKey = 0x7FF0000000000000L;                     // the key of +∞ and of NaN
    public static double KeyOf(double fitness);                              // NaN → +∞
    public static long OrderKey(double fitness);                             // the order of KeyOf, as an integer
    public static bool Precedes(long keyA, int indexA, long keyB, int indexB);
}

internal readonly record struct SuccessSums( // one chunk's sums, added in index order (S7)
    double Weight, double WeightedCr, double WeightedCrSquared,
    double WeightedF, double WeightedFSquared, double MaxCr)
{
    public const int Width = 6;
    public SuccessSums Add(double weight, double crossoverProbability, double mutationForce);
    public SuccessSums Combine(SuccessSums later);
}

internal static class AdaptationRules        // host and device (S7)
{
    public const double TerminalCr = -1.0;
    public const double InitialValue = 0.5;
    public static double WeightOf(ParameterRule rule, int outcome, double parentFitness, double trialFitness);
    public static double ScaleOf(double largestWeight, int count);          // S19
    public static void UpdateJadeMeans(SuccessSums sums, double adaptationRate, ref double meanCr, ref double meanF);
    public static bool UpdateShadeSlot(SuccessSums sums, bool lShade, ref double slotCr, ref double slotF);
}

internal enum MemoryRule { Shade = 0, LShade = 1 }

internal static class ArchiveRules           // host and device (S8)
{
    public static int SlotOf<TDraws>(ref TDraws draws, int fillPosition, int capacity) where TDraws : struct, IDrawSource;
    public static int Capacity(double archiveSizeRate, int populationSize);  // round half away from zero
}

internal static class LShadeSchedule         // host (S11)
{
    public const int MinimumPopulationSize = 4;
    public static int NextPopulationSize(int initialPopulationSize, long maxEvaluationNumber,
        long evaluationCount, int currentPopulationSize);
}

internal static class StagnationRule         // host and device (S12)
{
    public const double InitialLastBest = double.MinValue;
    public static bool Apply(double best, double threshold, int maxStagnationStreak, ref double lastBest, ref int streak);
}

internal static class BookkeepingKernels     // one entry point per pass, Index1D first
{
    public const int ChunkSize = 1024;           // the sums (S7)
    public const int StopSet = 0;                // the stop word: set, generation, streak
    public const int StopGeneration = 1;
    public const int StopStreak = 2;
    public const int StopLength = 3;
    public static int ChunkCount(int count);     // ⌈N / ChunkSize⌉
    public static int WideChunkSizeOf(int populationSize);          // max(32, 32·⌈⌈√N⌉/32⌉) (A4)
    public static int WideChunkCount(int count, int wideChunkSize); // ⌈N / wideChunkSize⌉, 0 for none
    // FillInts, FillDoubles; BestOfChunks, BestOfPartials; RankByCounting, LoadSortKeys,
    // BitonicStep; CountImproved, ScanImproved, PlaceImproved, CopyToArchive;
    // LargestWeights, SumSuccesses, Adapt; Compact; Stagnate. The wide passes (BestOfChunks,
    // CountImproved, PlaceImproved, LargestWeights) take the wide chunk size as an int
    // argument after the count; SumSuccesses takes it too, to know how many wide chunks'
    // largest weights it reads.
}

internal readonly record struct RankingTimes(double Counting, double Bitonic); // µs at one n

internal readonly record struct RankingMeasurement(int Count, RankingTimes Times); // one n the calibration timed

internal static class RankingCalibration     // decision 2 (A3)
{
    public const int Floor = 1024;           // N_init ≤ Floor: L = Floor, nothing timed
    public const int Ceiling = 8192;         // the largest n timed
    public const int UntimedLimit = 2048;    // L on the CPU accelerator
    public static int LimitOf(int populationSize, Func<int, RankingTimes> time);
}

internal sealed record BookkeepingTuning(int? RankingLimit = null, int? WideChunkSize = null);

internal sealed record BookkeepingPlan(
    int PopulationSize, int GenomeSize, SchemeKind Scheme, ParameterRule Rule,
    int ArchiveCapacity, int MemorySize, double AdaptationRate, bool LShade,
    double InitialMutationForce, double InitialCrossoverProbability,
    (double, int)? Stagnation)               // (threshold, max streak)
{
    public bool NeedsBestIndex { get; }      // best/1, best/2, current-to-best/1, a stop rule
    public bool NeedsRanking { get; }        // current-to-pbest/1
    public bool Adapts { get; }              // JADE, SHADE, L-SHADE
}

internal sealed class GenerationBookkeeping : IDisposable
{
    public GenerationBookkeeping(Accelerator accelerator, BookkeepingPlan plan, int seed);
    internal GenerationBookkeeping(Accelerator accelerator, BookkeepingPlan plan, int seed,
        BookkeepingTuning tuning);           // tests: forces L or c; nothing timed when L is
    public StrategyViews Views { get; }
    public int RankingLimit { get; }         // L: counting ranks count ≤ L
    public int WideChunkSize { get; }        // c(N_init), fixed for the run
    internal IReadOnlyList<RankingMeasurement> RankingMeasurements { get; } // what the calibration timed; empty if nothing
    public void AfterInitialization(PopulationViews views);
    public void AfterGeneration(ref PopulationViews views, int generation, int populationSize,
        int archiveCapacity, int nextPopulationSize, int nextArchiveCapacity);
    public void Dispose();
    // For the tests: Rank, RankByCounting, RankByBitonicNetwork, FindBest, SortLength.
}
```

- Every kernel reads the stop word first and returns when it is set.
- Every kernel a plan uses is loaded in the constructor, once, through `KernelLoader` with the
  largest extent it is launched with (checks A10, A2), and none afterwards: the two fills;
  with `NeedsRanking`, `RankByCounting` (N_init up to the ranking limit, or L-SHADE, whose
  population shrinks) and `LoadSortKeys` with `BitonicStep` (above it), both when the limit
  is timed (below); with `NeedsBestIndex`,
  `BestOfChunks` and `BestOfPartials`; with an archive, the four archive passes; with
  `Adapts`, `SumSuccesses` and `Adapt`, and `LargestWeights` under SHADE; `Compact` under
  L-SHADE; `Stagnate` with a stagnation rule. The seams `Rank`, `RankByCounting`,
  `RankByBitonicNetwork` and `FindBest` need their kernels in the plan and throw
  `InvalidOperationException` otherwise.
- SHADE's weights are scaled before they are summed (check S19, added 2026-10-08):
  `ScaleOf(largestWeight, count)` is `largestWeight` when it exceeds
  `double.MaxValue / (2·count)`, else 1.0. `LargestWeights` (one thread per wide chunk,
  SHADE and L-SHADE only, one double per wide chunk in a buffer allocated under SHADE only)
  runs before `SumSuccesses`, which takes the maximum over the wide chunks' largest weights
  and adds `weight / scale`, skipping the division when the scale is 1.0 (it changes no
  bit); JADE's scale is 1.
- The sums (`SumSuccesses`, then `Adapt` over its partials) run in chunks of 1 024
  individuals, S7's order. The passes whose result cannot depend on their order, the best
  index (`BestOfChunks`, then `BestOfPartials` over the wide chunks in order), the improved
  count, the scan and the placement of the archive, and `LargestWeights`, run in wide chunks
  of `c = WideChunkSizeOf(N_init)` = `max(32, 32·⌈⌈√N⌉/32⌉)`: more partials, the same combine
  (a pass of c serial steps and a closing pass over N / c partials are shortest together at
  c ≈ √N). The instance fixes `WideChunkSize` at construction (L-SHADE's N only falls, so
  `⌈N / c⌉` never outgrows the partial buffers, sized for `N_init`); the kernels take it as
  an argument. A best-index thread keeps its incumbent's fitness in a register.
- `OrderKey` maps NaN, whatever its sign and payload, to `InfinityKey` and −0 to +0's key, a
  non-negative value to its bits and a negative one to the negation of its magnitude's
  bits, so that integer order is `KeyOf`'s; it uses no floating-point operation, which a
  consumer GPU runs at a fraction of its integer rate. The ranking kernels compare keys with
  `Precedes`; the bitonic network's key buffer is `long`s.
- Ranking by counting up to N = L (`RankingLimit`), the bitonic network above it, over N
  rounded up to a power of two with `InfinityKey` at the end. `LimitOf` asks `time` at
  n = 2 048, 4 096, 8 192, each capped at `populationSize`, ascending and distinct; L is the
  last n at which `Counting ≤ Bitonic`, stopping at the first n at which it is not, and
  `Floor` when that is the first (a time that is `NaN` is not "not slower"). `populationSize
  ≤ Floor` returns `Floor` without asking.
- The constructor times, on CUDA and OpenCL, when the plan ranks, no limit is forced and
  N_init > `Floor`: after the fills, both rankings over a scratch buffer of N_init zeros
  (zeroed, released after), at each n `LimitOf` asks, each call followed by a
  synchronisation, host clock, the median of 3 after 1 warm-up, in microseconds
  (`RankingMeasurements` keeps what it measured). Both rankings are loaded before it
  (counting for N_init, the network for its sort length) and the sort keys and the ranking
  buffer sized for the network, whatever L comes out. Without timing, loads follow L: counting
  when N_init ≤ L or under L-SHADE, the network when N_init > L. L is `UntimedLimit` on the
  CPU accelerator, `Floor` on CUDA and OpenCL for N_init ≤ `Floor`, else what a test forced
  through `BookkeepingTuning` (a forced L times nothing).
- Results never depend on L or on c (S9; A4's order-independence); P0's hash does not move.
- `BookkeepingTuning` is for the tests: a member below 1 throws `ArgumentOutOfRangeException`.
- The control block of the stop rule is the only buffer the host reads during a run:
  every 16 generations and before each observer call.
