# BOOT.md — LocalSearch

## Purpose

The seam for hybrid (memetic) optimization: a local optimizer polishes the best
individual between generations and feeds the improvement back into the population, while
the adaptive variant's own state is left as it is.

## Invariants

- **The refiner runs single-threaded, between generations.** No worker runs, so no
  synchronisation. Held by the engine; stated by the contract.
- **Evaluations made by the refiner count toward the budget** — by contract only; the
  engine cannot see them. Held for the test refiner by
  `LocalSearchHookTests.RefinerEvaluationsAreFoldedIntoEvaluationCount`; any other
  refiner is on its honour.

## Dependencies

- [Models](../Models/API.md) — `ProblemContext`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition: none.

## Acceptance criteria

- [x] A refiner is invoked on its interval and its improvement reaches the result:
      2026-10-02, `LocalSearchHookTests` (integration, local run, part of 22 of 22 for
      slice 4).
- [ ] ⚠ The refiner receives the whole mutable `ProblemContext`: it can swap
      populations, rewrite the archive or the best index. The generation hook was
      narrowed for exactly this reason (`293b2b1`); this one was not.
- [ ] ⚠ Counting evaluations is left to the implementation; a refiner that forgets
      makes `LimitEvaluationNumberTerminationStrategy` stop late, undetected.
- [ ] ⚠ Part of the `Models` cycle: `ProblemContext` holds an `ILocalSearchRefiner`.

## Taboos

- **No randomness taken from the engine's workers.** The refiner runs on the
  orchestrator thread; a worker's provider used there would break that worker's seeded
  stream (the README: a refiner "owns its randomness entirely and must seed itself").
