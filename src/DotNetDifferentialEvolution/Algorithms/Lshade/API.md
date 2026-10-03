# API.md — Algorithms/Lshade

Namespace: `DotNetDifferentialEvolution.Algorithms.Lshade`. L-SHADE (Tanabe & Fukunaga,
2014): SHADE plus Linear Population Size Reduction. Everything not listed here is
internal structure and may change.

## Strategy ✅

```csharp
public class LShadeStrategy : ShadeStrategy
{
    public const int MinimumPopulationSize = 4;

    public LShadeStrategy(int initialPopulationSize, long maxEvaluationNumber,
        double archiveSizeRate, int memorySize,
        int minPopulationSize = MinimumPopulationSize);

    protected override bool UseTerminalCr { get; }      // true
    protected override bool UseLehmerCrMean { get; }    // true
    public override void AfterGeneration(GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords);
}
```

`AfterGeneration` does SHADE's bookkeeping, then the reduction: planned size
`round((N_min − N_init) · min(1, evals / maxEvals) + N_init)` (half away from zero),
clamped to `[N_min, current]`. If smaller than the current size, the best survivors (by
the ranking, rebuilt here only if the mutation strategy did not declare one) are compacted
to the front in ascending fitness order, the live size is narrowed, the ranking becomes
the identity, and the archive capacity becomes `round(archiveSizeRate · N_new)`, with the
archive truncated to it.

## Errors

| Situation | Behaviour |
|---|---|
| `minPopulationSize < 4` | `ArgumentOutOfRangeException` |
| `initialPopulationSize < minPopulationSize` | `ArgumentException` |
| `maxEvaluationNumber <= 0` | `ArgumentOutOfRangeException` |
| `archiveSizeRate < 0` | `ArgumentOutOfRangeException` |

## Side effects

Rewrites the current population's front, the live size, the ranking and the archive
capacity between generations.

## Out of scope

- Matching the evaluation budget to the stop rule: `LShadeVariant.Validate`
  ([Variants](../../Variants/API.md)).
