# API.md — GPU termination contract

Namespace: `DotNetDifferentialEvolution.GPU.TerminationStrategies.Interfaces`. The
host-side rule that stops a run. Everything not listed here is internal structure and
may change.

## Contract ✅

```csharp
public interface ITerminationStrategy
{
    bool IsMustTerminate(Accelerator device, int generation, HostPopulation population);
}
```

Called on the host after every generation, after the device has synchronised and the
populations have been swapped: `generation` counts from 1, `population` is the one that
just became current. Returning `true` ends the run. At least one generation always runs
before the first call. Anything the rule reads from `population` it must copy from the
device itself.

## Errors

| Situation | Behaviour |
|---|---|
| The rule throws | The exception propagates out of the run |

## Side effects

None required by the contract.

## Out of scope

- Cancellation: the controller checks the token separately, after the rule.
