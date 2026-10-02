# API.md — GPU SelectionStrategies

Namespace: `DotNetDifferentialEvolution.GPU.SelectionStrategies`. The package's
selection: greedy one-to-one, the trial survives only if strictly better. Everything not
listed here is internal structure and may change.

## Greedy selection ✅

```csharp
public readonly struct SelectionStrategy : ISelectionStrategy
{
    public void Select(
        int index,
        DevicePopulation currentPopulation,
        DevicePopulation nextPopulation,
        DevicePopulation trialPopulation);
}
```

Implements [the contract](Interfaces/API.md). If
`trial.FitnessFunctionValues[index] < current.FitnessFunctionValues[index]`, the trial's
genes and fitness are copied into `next[index]`; otherwise the parent's are. Therefore:

- a tie keeps the parent;
- a `NaN` trial never survives;
- a `NaN` parent is never replaced (`x < NaN` is false for every `x`).

## Errors

None raised; kernel code.

## Side effects

Writes individual `index` of `nextPopulation`.

## Out of scope

- Constraint handling, crowding, any rule other than greedy.
