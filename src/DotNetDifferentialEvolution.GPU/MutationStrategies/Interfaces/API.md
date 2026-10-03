# API.md — GPU mutation contract

Namespace: `DotNetDifferentialEvolution.GPU.MutationStrategies.Interfaces`. The
kernel-side rule that builds one trial vector. Everything not listed here is internal
structure and may change.

## Contract ✅

```csharp
public interface IMutationStrategy<in TRandomGenerator>
    where TRandomGenerator : struct, IRandomGenerator
{
    void Mutate(
        int index,
        DevicePopulation currentPopulation,
        DevicePopulation trialPopulation,
        TRandomGenerator random);
}
```

Implemented by a **struct** (the controller constrains it) and called first in the run
kernel by thread `index`. It must write all genes of `trialPopulation.Individuals[index,
*]`, reading any individual of `currentPopulation` it needs; it does not write fitness
(the objective does, right after). `random` is drawn with index `index`
([random contract](../../RandomGenerators/Interfaces/API.md)).

## Errors

| Situation | Behaviour |
|---|---|
| An implementation leaves trial genes unwritten | Not detected; they keep the values of an earlier generation's trial |

## Side effects

Writes the genes of one trial individual; advances the random stream `index`.

## Out of scope

- Fitness evaluation and survival: the objective and the selection rule.
