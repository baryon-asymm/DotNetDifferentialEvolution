# API.md — GPU TerminationStrategies

Namespace: `DotNetDifferentialEvolution.GPU.TerminationStrategies`. The package's stop
rules. Everything not listed here is internal structure and may change.

## Generation limit ✅

```csharp
public class MaxGenerationStrategy : ITerminationStrategy
{
    public MaxGenerationStrategy(int maxGenerationCount);
    public bool IsMustTerminate(Accelerator device, int generation, HostPopulation population);
}
```

Implements [the contract](Interfaces/API.md): `true` when `generation >=
maxGenerationCount`. Since the first call comes after generation 1, a run makes exactly
`maxGenerationCount` generations when that is at least 1, and one generation otherwise.
It does not read the population.

## Errors

None: any `int` is accepted.

## Side effects

None.

## Out of scope

- Stopping on fitness, stagnation, evaluation count or wall time.
