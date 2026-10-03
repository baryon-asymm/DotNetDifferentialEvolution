# API.md — AlgorithmExecutors

Namespace: `DotNetDifferentialEvolution.AlgorithmExecutors`. The per-worker body of a
generation. Everything not listed here is internal structure and may change. The
contract is in [Interfaces](Interfaces/API.md).

## Executor ✅

```csharp
public class AlgorithmExecutor : IAlgorithmExecutor
{
    public AlgorithmExecutor(IMutationStrategy mutationStrategy,
        ISelectionStrategy selectionStrategy, ProblemContext context);
    public void Execute(int workerId, out int bestHandledIndividualIndex);
}
```

The constructor creates one `SeededRandomProvider` per worker, seeded
`rootSeed + workerId`, where `rootSeed` is `context.RandomSeed` or, for an unseeded run,
one draw of `Random.Shared`.

`Execute(k)` handles individuals `k, k + W, k + 2W, …` below the live size (`W` =
`WorkersCount`). For each individual `i`:

1. F and CR from the context's provider (`NaN` for both without one);
2. a `MutationContext` over the current population, built on the stack;
3. `Mutate` into a per-call `stackalloc` trial buffer;
4. `Evaluate(workerIndex: k, genes: trial)`;
5. `SelectSurvivor` into the next population at `i`; the outcome, F, CR and both fitness values
   go into `TrialRecords[i]`;
6. track the best written individual by the engine's comparison rule.

## Errors

| Situation | Behaviour |
|---|---|
| `null` mutation strategy or context | `ArgumentNullException` |
| A strategy declaring `ControlParameters` with no provider in the context | `InvalidOperationException`, in the constructor |
| An exception in the objective or a strategy | Propagates out of `Execute` |

## Side effects

Writes the next population and the trial records at the worker's indices; advances the
worker's generator.

## Out of scope

- The archive, the ranking, the swap and the best of the whole population: the
  orchestrator and the generation strategy.
