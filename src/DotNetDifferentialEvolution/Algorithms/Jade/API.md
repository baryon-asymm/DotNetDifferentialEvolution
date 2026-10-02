# API.md — Algorithms/Jade

Namespace: `DotNetDifferentialEvolution.Algorithms.Jade`. JADE's parameter adaptation and
archive (Zhang & Sanderson, 2009). Everything not listed here is internal structure and
may change.

## Strategy ✅

```csharp
public class JadeStrategy : AdaptiveStrategyBase, IControlParameterProvider, IGenerationStrategy
{
    public const double DefaultAdaptationRate = 0.1;   // c
    public const double DefaultInitialMean = 0.5;

    public JadeStrategy(int populationSize, double adaptationRate = DefaultAdaptationRate,
        double initialMean = DefaultInitialMean);

    public void GetControlParameters(int individualIndex, BaseRandomProvider randomProvider,
        out double mutationForce, out double crossoverProbability);
    public void AfterGeneration(GenerationContext context, ReadOnlySpan<TrialRecord> trialRecords);
}
```

Per trial: CR ~ N(μCR, 0.1) clamped to `[0, 1]`; F ~ Cauchy(μF, 0.1), redrawn while
`<= 0`, capped at 1. After the generation: the archive update of
[Common](../Common/API.md), then, over **improving** trials only,
`μCR ← (1 - c) μCR + c · mean(S_CR)` and `μF ← (1 - c) μF + c · Lehmer(S_F)`
(`Σ F² / Σ F`). No improving trial leaves both means unchanged.

## Errors

| Situation | Behaviour |
|---|---|
| `null` context | `ArgumentNullException` |

## Side effects

Updates μF, μCR and the archive between generations.

## Out of scope

- p-best mutation: `CurrentToPBestMutationStrategy`, installed by
  [Variants](../../Variants/API.md).
