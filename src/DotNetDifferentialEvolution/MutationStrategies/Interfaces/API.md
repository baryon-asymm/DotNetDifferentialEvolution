# API.md — mutation contract

Namespace: `DotNetDifferentialEvolution.MutationStrategies.Interfaces`. How a strategy
builds one trial and what it tells the engine it needs. Everything not listed here is
internal structure and may change.

## Contract ✅

```csharp
public interface IMutationStrategy
{
    int MinimumPopulationSize => 2;
    MutationRequirements Requirements => MutationRequirements.ControlParameters;
    void Mutate(in MutationContext context);
}
```

`Mutate` writes the whole trial (mutation, then crossover and repair) into
`context.TrialIndividual` from the [`MutationContext`](../API.md) the engine builds on the
worker. `MinimumPopulationSize` is the smallest population for which the strategy can
draw its distinct individuals; the builder refuses a smaller one. `Requirements` declares
what the engine must provision; the builder validates it and the engine maintains it.

## Requirements ✅

```csharp
[Flags]
public enum MutationRequirements
{
    None = 0,
    ControlParameters = 1 << 0,
    FitnessRanking = 1 << 1,
    Archive = 1 << 2,
    BestIndividual = 1 << 3
}
```

| Flag | What the strategy reads | What the engine does |
|---|---|---|
| `ControlParameters` | `MutationForce`, `CrossoverProbability` | the builder rejects a configuration without an `IControlParameterProvider` (without one both are `NaN`) |
| `FitnessRanking` | `FitnessSortedIndices` | re-ranks the live population every generation, after the swap |
| `Archive` | `Archive`, `ArchiveSize` | a capability: an empty archive is valid |
| `BestIndividual` | `BestIndividualIndex` | always supplied; the flag documents the dependency |

## Errors

Implementation-defined.

## Side effects

`Mutate` writes the trial buffer and draws from the worker's random provider.

## Out of scope

- Selection and evaluation of the trial.
