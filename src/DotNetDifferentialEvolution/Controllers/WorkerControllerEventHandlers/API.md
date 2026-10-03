# API.md — WorkerControllerEventHandlers

Namespace: `DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers`. The
orchestrator: everything that happens between two generations. Everything not listed
here is internal structure and may change. The contract it implements is in
[Interfaces](Interfaces/API.md).

## Orchestrator ✅

```csharp
public class OrchestratorWorkerHandler : IWorkerPassLoopDoneHandler
{
    public OrchestratorWorkerHandler(ReadOnlyMemory<WorkerController> slaveWorkers,
        ProblemContext context);
    public void Handle(WorkerController masterWorker, out bool shouldTerminate);
    public Task<Population> GetResultPopulationTask();
    internal void UseCancellationToken(CancellationToken cancellationToken);
    internal void CancelBeforeStart(CancellationToken cancellationToken);
}
```

`Handle`, on the master's thread, in this order:

1. wait for every slave to finish its stripe or fail (spin-then-yield, never sleep);
2. if any worker failed: stop the slaves, fault the result task with an
   `AggregateException` of all worker exceptions, terminate;
3. otherwise swap populations; add the live size to `EvaluationCount`; re-rank if the
   mutation strategy declared `FitnessRanking`; call the generation strategy;
4. best index: the reduction of the workers' bests when there is no generation strategy,
   a full scan of the live population when there is one;
5. generation number + 1; the local-search refiner on its interval;
6. stamp the representative population; call the observer; ask the stop rule;
7. stop → stop the slaves, move the cursor to the best, complete the result task with
   the population; else cancellation requested → stop the slaves, complete the task as
   canceled; else permit every worker to start the next generation.

## Errors

| Situation | Behaviour |
|---|---|
| `null` context or master | `ArgumentNullException` |
| Any worker throws | Result task faulted with `AggregateException`; slaves stopped |
| A hook, the observer or the stop rule throws | The master worker records it as its own failure and calls `Handle` again, which faults the task as above |

## Side effects

Swaps and stamps the context, advances `EvaluationCount`, stops threads, completes the
result task.

## Out of scope

- Building the context and the workers: the package root.
