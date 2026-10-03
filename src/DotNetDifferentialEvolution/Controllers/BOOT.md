# BOOT.md — Controllers

## Purpose

The threading of the CPU engine: one long-lived thread per worker, released one
generation at a time, with the last worker doubling as the orchestrator through its
handler. The node owns the worker; its child owns what happens at the barrier.

## Invariants

- **A worker is a failure boundary.** Any exception from the objective or a strategy is
  captured and handed to the orchestrator, never left to crash the thread. Held by the
  `catch (Exception)` (CA1031 suppressed with that reason) and
  `WorkerControllerTests.PropagatesFitnessFunctionExceptionAndStops`.
- **The release handshake is two volatile flags** (`_passLoopPermitted`,
  `_isPassLoopCompleted`); the orchestrator's read of a worker's non-volatile exception
  field relies on the volatile read before it as an acquire fence (`1993d46`). Held by
  the code.
- **The permission wait yields but never sleeps.** Held by `SpinOnce(-1)` and the
  oversubscription test.
- **Stop and start can be repeated and leave a consistent state.** Held by
  `WorkerControllerTests.RandomStopAndStartKeepsConsistentStateThenTerminates` (`Slow`).
- **Repeated build-run-dispose leaks neither controllers nor threads.** Held by
  `WorkerLifecycleTests` and `ResourceUsageTests.RepeatedRunsDoNotGrowManagedHeapUnbounded`
  (`Slow`).

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
