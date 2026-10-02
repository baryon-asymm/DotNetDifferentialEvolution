# API.md — Algorithms/Common

Namespace: `DotNetDifferentialEvolution.Algorithms.Common`. What the JADE family shares:
the external archive and a private fitness ranking. Everything not listed here is
internal structure and may change.

## Base class ✅

```csharp
public abstract class AdaptiveStrategyBase
{
    protected AdaptiveStrategyBase(int populationSize);
    protected BaseRandomProvider RandomProvider { get; }
    public void UseRandomProvider(BaseRandomProvider randomProvider);
    protected void UpdateArchive(GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords, int currentPopulationSize);
    protected void RebuildSortedIndices(GenerationContext context, int currentPopulationSize);
}
```

- `RandomProvider` starts as the family's default `RandomProvider`; `UseRandomProvider`
  (the builder's call for a seeded run) replaces it.
- `UpdateArchive`: for every **improving** trial, copies its displaced parent (from
  `context.DiscardedParents`) into the archive; appends while there is room, otherwise
  overwrites a uniformly random slot. Capacity is `min(ArchiveCapacity, buffer length)`;
  zero or negative means no archive.
- `RebuildSortedIndices`: ranks the live population into `context.FitnessSortedIndices`
  with the engine's sort (`NaN` last).

## Errors

| Situation | Behaviour |
|---|---|
| `null` provider or context | `ArgumentNullException` |

## Side effects

Writes the archive and its size; writes the ranking; draws from `RandomProvider`.

## Out of scope

- Parameter adaptation: each derived variant.
