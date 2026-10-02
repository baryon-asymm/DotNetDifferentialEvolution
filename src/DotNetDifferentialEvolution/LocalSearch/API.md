# API.md — LocalSearch

Namespace: `DotNetDifferentialEvolution.LocalSearch`. The memetic hook: refine the
population in place between generations. Everything not listed here is internal
structure and may change.

## Refiner ✅

```csharp
public interface ILocalSearchRefiner
{
    void Refine(ProblemContext context, int generationNumber);
}
```

Called single-threaded on the orchestrator thread every `LocalSearchInterval`
generations (set by `WithLocalSearch(refiner, everyNGenerations)`), after the swap and
after the best individual has been identified. Receives the whole
[`ProblemContext`](../Models/API.md): typically it reads the best individual through
`context.CurrentPopulation.GenesOf(context.BestIndividualIndex)`, improves it and writes
genes and fitness back. It **must** add every evaluation it performs to
`context.EvaluationCount`, or an evaluation budget is exceeded silently.

## Errors

Implementation-defined; an exception propagates out of the run.

## Side effects

Whatever the refiner writes into the context.

## Out of scope

- A local optimizer: the package ships none; Nelder–Mead lives in another package of the
  family.
- Randomness: a refiner owns its own and must seed it itself.
