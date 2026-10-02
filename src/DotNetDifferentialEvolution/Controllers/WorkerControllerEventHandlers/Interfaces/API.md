# API.md — pass-loop handler contract

Namespace:
`DotNetDifferentialEvolution.Controllers.WorkerControllerEventHandlers.Interfaces`. How the
master worker hands control to the orchestrator at the end of each generation.
Everything not listed here is internal structure and may change.

## Contract ✅

```csharp
public interface IWorkerPassLoopDoneHandler
{
    void Handle(WorkerController masterWorker, out bool shouldTerminate);
}
```

Called on the master worker's thread after it finished its own stripe, and once more if
that worker caught an exception. `shouldTerminate = true` ends the master's loop.

## Errors

Implementation-defined.

## Side effects

Implementation-defined; the orchestrator does the whole between-generations work here.

## Out of scope

- Slave workers: they are created without a handler.
