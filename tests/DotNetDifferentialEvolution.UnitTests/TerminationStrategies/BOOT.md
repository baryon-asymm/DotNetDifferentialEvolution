# BOOT.md — UnitTests/TerminationStrategies

## Purpose

Level U0 for the stop rules: the boundary of each limit, and the stagnation streak's
counting across calls.

## Invariants

- **Each limit is checked at 0, limit − 1, limit and limit + 1.**
- **Generations are simulated by calling the rule repeatedly** on one population whose
  fitness array the test changes in between ([TestSupport](../TestSupport/API.md)).

## Dependencies

- [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — under test.
- [TestSupport](../TestSupport/API.md) — `PopulationFactory`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 11 cases in 3 classes.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. `>` instead of `>=` in the
      generation limit turned `TerminatesOnceTheGenerationLimitIsReached` red.
- [ ] ⚠ The stagnation rule's handling of a `NaN` best value is not tested.

## Taboos

- **No limit test without the exact boundary case.**
