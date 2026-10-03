# API.md — Helpers

Namespace: `DotNetDifferentialEvolution.Helpers`. The engine's rules for ranking fitness
values: comparing two of them, and ordering a population. Everything not listed here is
internal structure and may change.

## Population ranking ✅

```csharp
public static class PopulationSortHelper
{
    public static void SortIndicesByFitness(
        Span<int> sortedIndices,
        ReadOnlySpan<double> populationFfValues,
        int count,
        Span<double> keyBuffer);
}
```

Fills `sortedIndices[0..count)` with `0..count-1` ordered by ascending fitness, best
first. `NaN` sorts **last** (its key is replaced by `+∞`); the fitness values themselves
are not touched. `keyBuffer` is scratch of at least `count` doubles. The sort is
`Span.Sort` (introsort), so the order of equal fitness values is not specified.

## Comparison rule — internal to the assembly ✅

```csharp
internal static class FitnessComparisonHelper
{
    public static bool IsBetter(double candidateFfValue, double incumbentFfValue);
    public static bool IsBetterOrEqual(double candidateFfValue, double incumbentFfValue);
}
```

- `IsBetter(c, i)`: `c < i`, or `i` is `NaN` and `c` is not.
- `IsBetterOrEqual(c, i)`: `c <= i`, or `i` is `NaN` and `c` is not. Two `NaN`s are
  **not** equal here.

Lower is better. `NaN` is worse than every real value; `+∞` is an ordinary real value.

## Errors

| Situation | Behaviour |
|---|---|
| `count` larger than any of the three spans | `ArgumentOutOfRangeException` from `Slice` |

## Side effects

`SortIndicesByFitness` writes `sortedIndices` and `keyBuffer`; nothing else.

## Out of scope

- Deciding survival or success: the selection strategy applies these rules.
