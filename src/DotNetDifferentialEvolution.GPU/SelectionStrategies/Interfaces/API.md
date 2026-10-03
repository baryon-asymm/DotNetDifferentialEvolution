# API.md — GPU selection contract

Namespace: `DotNetDifferentialEvolution.GPU.SelectionStrategies.Interfaces`. The
kernel-side rule that decides which of parent and trial survives. Everything not listed
here is internal structure and may change.

## Contract ✅

```csharp
public interface ISelectionStrategy
{
    void Select(
        int index,
        DevicePopulation currentPopulation,
        DevicePopulation nextPopulation,
        DevicePopulation trialPopulation);
}
```

Implemented by a **struct** (the controller constrains it) and called inside the run
kernel by thread `index`, after the trial's fitness has been evaluated. It must write
individual `index` of `nextPopulation` completely, genes and fitness, from either the
current or the trial population. It must not write any other index.

## Errors

| Situation | Behaviour |
|---|---|
| An implementation leaves `nextPopulation[index]` partly unwritten | Not detected; the next generation inherits stale data from two generations back |

## Side effects

Writes one individual of `nextPopulation`.

## Out of scope

- Population-wide selection (ranking, elitism across individuals): one thread sees one
  index, and threads of a launch run in any order.
