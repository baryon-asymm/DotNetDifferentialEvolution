# API.md — termination contract

Namespace: `DotNetDifferentialEvolution.TerminationStrategies.Interfaces`. The rule that
stops a CPU run. Everything not listed here is internal structure and may change.

## Contract ✅

```csharp
public interface ITerminationStrategy
{
    bool ShouldTerminate(Population population);
}
```

Asked by the engine with the representative [`Population`](../../Models/API.md) — its
generation number, evaluation count, best index and live size stamped for the moment of
the call. `true` ends the run.

## Errors

Implementation-defined.

## Side effects

None required; an implementation that moves the population's cursor moves it for every
holder of that population.

## Out of scope

- Cancellation: the engine observes the token separately, at the generation barrier.
