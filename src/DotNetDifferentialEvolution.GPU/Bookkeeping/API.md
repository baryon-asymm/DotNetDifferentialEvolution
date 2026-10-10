# API.md — DotNetDifferentialEvolution.GPU/Bookkeeping

Namespace: `DotNetDifferentialEvolution.GPU.Bookkeeping`. Internal to the assembly;
visible to the GPU test project and to ILGPU's runtime assembly. Consumed by the
package root.

## Internal to the assembly ✅

Built 2026-10-05 (checks S7–S12 of this node's [ACCEPTANCE.md](ACCEPTANCE.md); S6 and S14
of the package's [ACCEPTANCE.md](../ACCEPTANCE.md)); ranking by integer keys and the passes in
chunks of 32 built 2026-10-10 (checks A3 and A4).

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
    public const int WideChunkSize = 32;         // the order-independent passes (A4)
    public const int StopSet = 0;                // the stop word: set, generation, streak
    public const int StopGeneration = 1;
    public const int StopStreak = 2;
    public const int StopLength = 3;
    public static int ChunkCount(int count);     // ⌈N / ChunkSize⌉
    public static int WideChunkCount(int count); // ⌈N / WideChunkSize⌉
    // FillInts, FillDoubles; BestOfChunks, BestOfPartials; RankByCounting, LoadSortKeys,
    // BitonicStep; CountImproved, ScanImproved, PlaceImproved, CopyToArchive;
    // LargestWeights, SumSuccesses, Adapt; Compact; Stagnate.
}

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
    public const int CountingRankLimit = 2048;
    public GenerationBookkeeping(Accelerator accelerator, BookkeepingPlan plan, int seed);
    public StrategyViews Views { get; }
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
  with `NeedsRanking`, `RankByCounting` (N up to the counting limit, or L-SHADE, whose
  population shrinks) and `LoadSortKeys` with `BitonicStep` (above it); with `NeedsBestIndex`,
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
  of 32: more partials, the same combine. A best-index thread keeps its incumbent's fitness
  in a register.
- `OrderKey` maps NaN, whatever its sign and payload, to `InfinityKey` and −0 to +0's key, a
  non-negative value to its bits and a negative one to the negation of its magnitude's
  bits, so that integer order is `KeyOf`'s; it uses no floating-point operation, which a
  consumer GPU runs at a fraction of its integer rate. The ranking kernels compare keys with
  `Precedes`; the bitonic network's key buffer is `long`s.
- Ranking by counting up to N = 2 048 (`CountingRankLimit`), the bitonic network above it,
  over N rounded up to a power of two with `InfinityKey` at the end.
- The control block of the stop rule is the only buffer the host reads during a run:
  every 16 generations and before each observer call.
