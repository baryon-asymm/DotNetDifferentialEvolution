# BOOT.md — IntegrationTests/Concurrency

## Purpose

Level I2: the properties that exist only because the engine runs on several threads —
cancellation at the generation barrier, reproducibility with per-worker generators,
freedom from data races, progress when threads outnumber cores, and clean release of
workers, threads and memory.

## Invariants

- **Cancellation is triggered from inside a generation** (an observer on the
  orchestrator thread), not by a timer, so the barrier at which it is seen is fixed and
  the generation count can be asserted exactly.
- **Reproducibility is asserted per worker count**, never across counts: individual
  `i` draws from worker `i mod W`'s stream.
- **Race detection is by repetition**: 25 parallel runs must each reach the optimum.
  It catches corruption that spoils convergence, not a race that leaves the result
  correct.
- **Timing is never asserted**: the oversubscribed run has a generous 120 s timeout and
  checks only completion and correctness.
- **Leak checks use the authoritative counter first** (`WorkerController.GlobalWorkerCounter`)
  and coarse process measures second, with stated slack.

## Dependencies

- [DotNetDifferentialEvolution](../../../src/DotNetDifferentialEvolution/API.md) — the
  builder, `RunAsync`, `Dispose`.
- [Controllers](../../../src/DotNetDifferentialEvolution/Controllers/API.md) —
  `GlobalWorkerCounter`.
- [Interfaces](../../../src/DotNetDifferentialEvolution/Interfaces/API.md) — the
  observer that cancels and counts.
- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md),
  [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — the result and the stop rule.
- [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/API.md)
  — Sphere.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Relies on the assembly running its tests one at a time: thread counts and heap size
  are process-wide.

## Acceptance criteria

- [x] Green: 2026-10-02, 21 cases in 5 classes (cancellation 6, reproducibility 9,
      parallel 3, lifecycle 2, resources 1 `Slow`).
- [x] Non-degenerate: 2026-10-02, scratch clone of `a504474`. Ignoring cancellation at
      the barrier turned 3 cases red (`CancellingMidRunCompletesTheTaskAsCanceled` with
      both worker counts, `ACanceledRunDisposesWithoutHanging`); not seeding the
      generation hook turned both cases of
      `AnAdaptiveVariantIsReproducibleIncludingItsArchiveEviction` red.
- [ ] ⚠ `ParallelDeterminismTests` is named for determinism and asserts convergence; its
      runs are unseeded.
- [ ] ⚠ The leak and heap checks were not shown red: no mutation leaked a worker or
      memory in this pass.
- [ ] ⚠ `ACanceledRunDisposesWithoutHanging` says `Dispose` "joins" every worker
      thread; it waits until each has left its loop
      ([Controllers](../../../src/DotNetDifferentialEvolution/Controllers/API.md)).

## Taboos

- **No wall-clock assertion.** A timeout bounds a hang; it is not a performance claim.
- **No reproducibility claim across worker counts.**
