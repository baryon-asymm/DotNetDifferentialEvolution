# API.md — Algorithms/Jde

Namespace: `DotNetDifferentialEvolution.Algorithms.Jde`. jDE's self-adaptation (Brest et
al., 2006). Everything not listed here is internal structure and may change.

## Strategy ✅

```csharp
public class JdeStrategy : IControlParameterProvider, IGenerationStrategy
{
    public const double DefaultFAdaptationProbability = 0.1;     // tau1
    public const double DefaultCrAdaptationProbability = 0.1;    // tau2
    public const double DefaultMinMutationForce = 0.1;
    public const double DefaultMutationForceRange = 0.9;
    public const double DefaultInitialMutationForce = 0.5;
    public const double DefaultInitialCrossoverProbability = 0.9;

    public JdeStrategy(int populationSize,
        double initialMutationForce = DefaultInitialMutationForce,
        double initialCrossoverProbability = DefaultInitialCrossoverProbability,
        double fAdaptationProbability = DefaultFAdaptationProbability,
        double crAdaptationProbability = DefaultCrAdaptationProbability,
        double minMutationForce = DefaultMinMutationForce,
        double mutationForceRange = DefaultMutationForceRange);

    public void GetControlParameters(int individualIndex, BaseRandomProvider randomProvider,
        out double mutationForce, out double crossoverProbability);
    public void AfterGeneration(GenerationContext context, ReadOnlySpan<TrialRecord> trialRecords);
}
```

One object in both roles. Each individual `i` carries `(F_i, CR_i)`, initially the given
values. Per trial: with probability tau1, F is regenerated uniformly in
`[min, min + range)`, else `F_i`; with probability tau2, CR is regenerated uniformly in
`[0, 1)`, else `CR_i`. After the generation, every individual whose trial **replaced** it
(improvement or tie) takes the trial's F and CR.

## Errors

| Situation | Behaviour |
|---|---|
| `null` provider or context | `ArgumentNullException` |
| Index beyond the constructed population size | `IndexOutOfRangeException` |

## Side effects

`AfterGeneration` overwrites per-individual parameters; draws happen on the workers.

## Out of scope

- The mutation operator: jDE uses `RandMutationStrategy` ([Variants](../../Variants/API.md)).
