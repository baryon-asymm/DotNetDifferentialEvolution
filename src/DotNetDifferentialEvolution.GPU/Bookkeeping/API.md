# API.md — DotNetDifferentialEvolution.GPU/Bookkeeping

Namespace: `DotNetDifferentialEvolution.GPU.Bookkeeping`. Internal to the assembly;
visible to the GPU test project and to ILGPU's runtime assembly. Consumed by the
package root.

## Internal to the assembly ✅

Built 2026-10-05 (checks S7–S12 of this node's [ACCEPTANCE.md](ACCEPTANCE.md); S6 and S14
of the package's [ACCEPTANCE.md](../ACCEPTANCE.md)).

```csharp
internal static class FitnessOrder           // the one total order (S9, S10)
{
    public static double KeyOf(double fitness);                              // NaN → +∞
    public static bool Precedes(double keyA, int indexA, double keyB, int indexB);
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
    public const int ChunkSize = 1024;
    public const int StopSet = 0;                // the stop word: set, generation, streak
    public const int StopGeneration = 1;
    public const int StopStreak = 2;
    public const int StopLength = 3;
    public static int ChunkCount(int count);
    // FillInts, FillDoubles; BestOfChunks, BestOfPartials; RankByCounting, LoadSortKeys,
    // BitonicStep; CountImproved, ScanImproved, PlaceImproved, CopyToArchive;
    // SumSuccesses, Adapt; Compact; Stagnate.
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
    public const int CountingRankLimit = 8192;
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
- Chunks are 1 024 individuals; ranking by counting up to N = 8 192, the bitonic
  network above it, over N rounded up to a power of two with +∞ keys at the end.
- The control block of the stop rule is the only buffer the host reads during a run:
  every 16 generations and before each observer call.
