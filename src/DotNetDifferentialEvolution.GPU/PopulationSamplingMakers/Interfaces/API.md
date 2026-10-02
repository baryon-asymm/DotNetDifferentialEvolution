# API.md — GPU population sampling contract

Namespace: `DotNetDifferentialEvolution.GPU.PopulationSamplingMakers.Interfaces`. The
host-side source of the initial population. Everything not listed here is internal
structure and may change.

## Contract ✅

```csharp
public interface IPopulationSamplingMaker
{
    int GetPopulationSize();
    double[,] TakeSamples();
}
```

`GetPopulationSize()` is the number of individuals; the controller uses it as the
kernel extent of every launch. `TakeSamples()` returns the initial genes on the host,
`[individual, gene]`, and must have exactly `GetPopulationSize()` rows. Called once,
when the controller allocates device memory.

## Errors

| Situation | Behaviour |
|---|---|
| `TakeSamples()` returns a row count different from `GetPopulationSize()` | Not detected; kernels index outside the population or skip individuals |

## Side effects

None required by the contract.

## Out of scope

- Fitness: the samples carry genes only; fitness is evaluated on the device.
