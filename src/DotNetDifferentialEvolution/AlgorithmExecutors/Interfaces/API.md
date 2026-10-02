# API.md — executor contract

Namespace: `DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces`. One worker's share
of one generation. Everything not listed here is internal structure and may change.

## Contract ✅

```csharp
public interface IAlgorithmExecutor
{
    void Execute(int workerId, out int bestHandledIndividualIndex);
}
```

Called by worker `workerId` once per generation: build, evaluate and select the worker's
stripe of individuals, and report the index of the best individual it wrote.

## Errors

An exception propagates to the calling worker, which records it.

## Side effects

Writes the worker's stripe of the next population and of the trial records.

## Out of scope

- Threads, the barrier and the swap: [Controllers](../../Controllers/API.md).
