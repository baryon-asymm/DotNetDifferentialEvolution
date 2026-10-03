# API.md — Algorithms/Shade

Namespace: `DotNetDifferentialEvolution.Algorithms.Shade`. SHADE's success-history
parameter adaptation (Tanabe & Fukunaga, 2013), the base of L-SHADE. Everything not
listed here is internal structure and may change.

## Strategy ✅

```csharp
public class ShadeStrategy : AdaptiveStrategyBase, IControlParameterProvider, IGenerationStrategy
{
    public const int DefaultMemorySize = 100;           // H
    public const double DefaultInitialMemoryValue = 0.5;

    public ShadeStrategy(int populationSize, int memorySize = DefaultMemorySize,
        double initialMemoryValue = DefaultInitialMemoryValue);

    protected virtual bool UseTerminalCr { get; }       // false
    protected virtual bool UseLehmerCrMean { get; }     // false

    public void GetControlParameters(int individualIndex, BaseRandomProvider randomProvider,
        out double mutationForce, out double crossoverProbability);
    public virtual void AfterGeneration(GenerationContext context,
        ReadOnlySpan<TrialRecord> trialRecords);
}
```

Memory of `H` pairs `(M_F, M_CR)`. Per trial: a uniform slot `r`; CR ~ N(M_CR[r], 0.1)
clamped to `[0, 1]`, or exactly 0 if the slot is terminal (negative); F ~ Cauchy(M_F[r],
0.1), redrawn while `<= 0`, capped at 1.

After the generation: the archive update of [Common](../Common/API.md), then one slot
(round-robin) is overwritten from **improving** trials weighted by
`w = parent fitness − trial fitness`, skipping non-finite weights:
`M_F ← Σ w F² / Σ w F`; `M_CR ←` weighted arithmetic mean (SHADE), or weighted Lehmer
mean when `UseLehmerCrMean`, or the terminal value when `UseTerminalCr` and every
successful CR was 0 (or the slot already was terminal). No usable success leaves the
memory and the slot index unchanged.

## Errors

| Situation | Behaviour |
|---|---|
| `memorySize <= 0` | `ArgumentOutOfRangeException` |
| `null` provider or context | `ArgumentNullException` |

## Side effects

Updates the memory, its index and the archive between generations.

## Out of scope

- Population reduction: [Lshade](../Lshade/API.md).
