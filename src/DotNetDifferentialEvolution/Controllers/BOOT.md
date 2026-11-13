# BOOT.md — Controllers

## Purpose

The threading of the CPU engine: one long-lived thread per worker, released one
generation at a time, with the last worker doubling as the orchestrator through its
handler. The node owns the worker; its child owns what happens at the barrier.

## Invariants

- **A worker reports captured exceptions to the orchestrator.** The filter
  `IsCapturedForTheOrchestrator` lets `OutOfMemoryException` escape the worker thread.
  See `WorkerControllerTests.PropagatesFitnessFunctionExceptionAndStops`.
  ⚠ 2026-11-13: the previous invariant described an unconditional catch and a CA1031
  suppression. Source inspection and an out-of-memory exception thrown in a child-process
  objective confirmed the filter and process termination.
- **The release handshake is two volatile flags** (`_passLoopPermitted`,
  `_isPassLoopCompleted`); the orchestrator's read of a worker's non-volatile exception
  field relies on the volatile read before it as an acquire fence (`1993d46`). Held by
  the code.
- **The permission wait yields but never sleeps.** Held by `SpinOnce(-1)` and the
  oversubscription test.
- **Stop and start can be repeated and leave a consistent state.** Held by
  `WorkerControllerTests.RandomStopAndStartKeepsConsistentStateThenTerminates` (`Slow`).
- After starting the native thread, `Start` yields until the worker publishes readiness.
  A pending `Thread.Interrupt` stays pending through this handshake.
- **Repeated build-run-dispose leaks neither controllers nor threads.** Held by
  `WorkerLifecycleTests` and `ResourceUsageTests.RepeatedRunsDoNotGrowManagedHeapUnbounded`
  (`Slow`).
- An optimizer disposing during a generation leaves the worker's pass and stop flags
  to the orchestrator. `DisposeWhenStopped` releases idle controllers immediately
  and active controllers on loop exit. An external disposer joins the threads.

## Dependencies

- [AlgorithmExecutors/Interfaces](../AlgorithmExecutors/Interfaces/API.md) —
  `IAlgorithmExecutor`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Worker threads run at `ThreadPriority.Highest`; every wait must yield.

## Acceptance criteria

- [x] Worker lifecycle, failure capture and resource use pass: 2026-10-02,
      `WorkerControllerTests`, `WorkerLifecycleTests`, `ResourceUsageTests` (integration,
      full local run including `Slow`, 76 of 76; the `Slow` ones are not run by CI).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ `GlobalWorkerCounter` is process-wide mutable state on a public type.
- [ ] ⚠ The class has a finalizer that does nothing but mark disposal; it costs every
      instance a finalization without freeing anything.
- [ ] ⚠ `WorkerController` and its child's handler interface reference each other.

## Taboos

- **No sleeping wait in the worker loop.** It adds up to a millisecond to every
  generation's barrier (`1993d46`).
- **No uncaught exception on a worker thread.** It would kill the thread and leave the
  orchestrator waiting forever.
