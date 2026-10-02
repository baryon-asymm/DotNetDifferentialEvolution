# API.md — Controllers

Namespace: `DotNetDifferentialEvolution.Controllers`. The worker threads of a CPU run.
Everything not listed here is internal structure and may change. The orchestrator is in
[WorkerControllerEventHandlers](WorkerControllerEventHandlers/API.md).

## Worker ✅

```csharp
public class WorkerController : IDisposable
{
    public WorkerController(int workerId, IAlgorithmExecutor algorithmExecutor,
        IWorkerPassLoopDoneHandler? workerPassLoopDoneHandler = null);

    public bool IsRunning { get; }
    public bool HasException { get; }
    public Exception? Exception { get; }
    public static int GlobalWorkerCounter { get; }
    public int WorkerId { get; }
    public bool IsPassLoopCompleted { get; }
    public int BestHandledIndividualIndex { get; }

    public void Start(bool throwIfRunning = false);
    public void Stop(bool throwIfStopped = false);
    public void PermitToPassLoop();
    public void Dispose();
}
```

One dedicated thread (`ThreadPriority.Highest`, named `"{n}-DEWorkerThread_{id}"`). Its
loop: wait for permission (spin, then yield; never sleep), run `Execute(workerId)`,
publish its best index, mark the pass done, and — if it has a handler — call it, ending
the loop when told to. An exception is caught, stored in `Exception`, reported to the
handler, and ends the loop.

- `Start` launches the thread and waits until it runs; with `throwIfRunning`, a second
  start throws.
- `Stop` asks the loop to end and spins until the thread has left it.
- `PermitToPassLoop` releases one generation.
- `GlobalWorkerCounter` counts live controllers process-wide (incremented on
  construction, decremented on disposal).

## Errors

| Situation | Behaviour |
|---|---|
| `Start(true)` while running / `Stop(true)` while stopped | `InvalidOperationException` |
| Exception inside `Execute` | Captured, not thrown; see `HasException`, `Exception` |

## Side effects

Creates an OS thread per start; mutates a process-wide counter.

## Out of scope

- What happens between generations: [WorkerControllerEventHandlers](WorkerControllerEventHandlers/API.md).
