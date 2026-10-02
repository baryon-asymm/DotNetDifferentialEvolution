# API.md — SelectionStrategies

Namespace: `DotNetDifferentialEvolution.SelectionStrategies`. The built-in survival rule
of the CPU engine and the outcome type every rule reports. Everything not listed here is
internal structure and may change. The contract itself is in
[Interfaces](Interfaces/API.md).

## Outcome ✅

```csharp
public enum SelectionOutcome
{
    ParentKept = 0,
    TrialAccepted = 1,
    TrialImproved = 2
}
```

- `ParentKept` — the parent survived; the default value, so a zeroed record reads as
  "nothing happened".
- `TrialAccepted` — the trial replaced the parent without being strictly better (a tie
  under a rule that admits ties). In the population, not a success.
- `TrialImproved` — the trial replaced the parent and was strictly better. The outcome
  the archive and the parameter adaptation key on.

## Greedy selection ✅

```csharp
public class SelectionStrategy : ISelectionStrategy
{
    public SelectionStrategy(int genomeSize);
    public SelectionStrategy(int genomeSize, bool acceptsTies);
    public SelectionOutcome Select(int individualIndex, double trialIndividualFfValue,
        Span<double> trialIndividual, Span<double> populationFfValues,
        Span<double> population, Span<double> nextPopulationFfValues,
        Span<double> nextPopulation);
}
```

With trial fitness `u` and parent fitness `x`, using the engine's comparison rule
([Helpers](../Helpers/API.md): `NaN` worse than every real value):

| Case | `acceptsTies: true` (default) | `acceptsTies: false` |
|---|---|---|
| `u` better than `x` (incl. real `u` over `NaN` `x`) | `TrialImproved` | `TrialImproved` |
| `u == x`, both real | `TrialAccepted` | `ParentKept` |
| `u` worse, or `u` is `NaN` (incl. both `NaN`) | `ParentKept` | `ParentKept` |

The default follows SHADE (2013) Eq. (6) and L-SHADE (2014) Alg. 2 line 12;
`acceptsTies: false` follows JADE (2009) Table I line 20. The survivor's
`genomeSize` genes and fitness are copied into the next population at the same index.

## Errors

| Situation | Behaviour |
|---|---|
| `genomeSize` that does not match the populations' layout | Not checked; wrong slices are copied, or `ArgumentOutOfRangeException` from `Slice` |

## Side effects

Writes one individual and one fitness value of the next population.

## Out of scope

- Which variant uses which setting: the variants choose it.
