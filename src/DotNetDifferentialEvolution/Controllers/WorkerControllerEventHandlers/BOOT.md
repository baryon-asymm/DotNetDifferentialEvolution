# BOOT.md — WorkerControllerEventHandlers

## Purpose

The generation barrier and everything done at it: the swap, the evaluation count, the
ranking, the adaptive hook, the best index, local search, the observer, the stop rule,
cancellation, and the result. It is the one place where the run is quiescent.

## Invariants

- **Between-generation work happens only when every worker has finished its stripe.**
  Held by the wait loop; no worker is permitted to start until `Handle` permits all.
- **Cancellation is observed only at the barrier.** The population is then consistent
  and no thread is mid-generation; a run stops within about one generation of the
  request (`de15b2d`). Held by `CancellationTests` (five cases, including an
  already-canceled token and disposal after cancellation).
- **Any worker's exception fails the whole run, with every worker's exception.** Held
  by `WorkersOrchestratorTests.FitnessFunctionExceptionPropagatesFromAnyWorkerAndStopsAll`.
- **The ranking is engine state, rebuilt before the hook runs** when the strategy
  declared it (`7a7649f`). Held by `FitnessRankingMaintenanceTests`.
- **A `NaN` individual is never reported best**, by either the reduction or the scan
  (`66fd1f3`). Held by `NaNFitnessTests`.
- **The barrier wait yields but never sleeps.** `SpinOnce(sleep1Threshold: -1)`; held by
  the code and by `ParallelDeterminismTests.OversubscribedWorkerCountCompletesAndConverges`.
- **Evaluations are counted per generation as the live size before the hook shrinks
  it.** The initial population's evaluations are counted by the builder. Held by the
  code; the L-SHADE schedule depends on it.

## Dependencies

- [Controllers](../API.md) — `WorkerController`, declared in the parent's directory.
- [GenerationStrategies](../../GenerationStrategies/API.md) — `GenerationContext`,
  `IGenerationStrategy`.
- [Helpers](../../Helpers/API.md) — `FitnessComparisonHelper`, `PopulationSortHelper`.
- [Models](../../Models/API.md) — `ProblemContext`, `Population`.
- [MutationStrategies/Interfaces](../../MutationStrategies/Interfaces/API.md) —
  `MutationRequirements`.
- [Interfaces](../../Interfaces/API.md) — `IPopulationUpdatedHandler`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [LocalSearch](../../LocalSearch/API.md) — `ILocalSearchRefiner`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [TerminationStrategies/Interfaces](../../TerminationStrategies/Interfaces/API.md) — `ITerminationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Runs on the master worker's thread, single-threaded; the hooks it calls rely on that.

## Acceptance criteria

- [x] Orchestration, cancellation, failure propagation, ranking and `NaN` handling pass:
      2026-10-02, `WorkersOrchestratorTests`, `CancellationTests`,
      `FitnessRankingMaintenanceTests`, `NaNFitnessTests` (integration, full local run
      including `Slow`, 76 of 76).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ With a generation strategy the best index comes from a full scan; without one,
      from the workers' reduction. Two code paths for one quantity.
- [ ] ⚠ An exception from a hook, the observer or the stop rule is caught by the master
      worker as its own, which calls `Handle` again; that call stops the slaves and
      faults the task with an `AggregateException`, so a bug in a consumer's observer
      reads like a worker failure (by reading the code, not tested).

## Taboos

- **No `Thread.Sleep(1)` at the barrier.** `SpinWait.SpinOnce()` escalates to it and
  measured 4.3–1471 µs per barrier against 0.59–15.6 µs without (`1993d46`).
- **No bare spin at top priority.** One worker more than available cores turned 0.048 s
  into 4.863 s on 4 pinned cores; 72 workers on 36 cores went from 15 s to 295 ms with
  the cooperative wait (`1993d46`).
- **No cancellation outside the barrier.** Interrupting a worker mid-stripe leaves trial
  buffers half-written (`de15b2d`).
- **No ranking left to the adaptive hooks.** Hand-wired p-best runs ranked the initial
  population for the whole run (`7a7649f`).
