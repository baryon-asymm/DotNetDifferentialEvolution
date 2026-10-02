# BOOT.md — TerminationStrategies

## Purpose

The three stop rules shipped with the CPU package: a generation count, an evaluation
budget (the natural criterion for L-SHADE) and a stagnation streak on the best fitness.

## Invariants

- **The limit rules are pure functions of the stamped population.** Held by the code
  and by `LimitGenerationNumberTerminationStrategyTests`,
  `LimitEvaluationNumberTerminationStrategyTests`.
- **The stagnation streak resets only on a change larger than the threshold.** Held by
  `StagnationStreakTerminationStrategyTests` (`ImprovementGreaterThanThresholdResetsTheStreak`,
  `ImprovementSmallerThanThresholdDoesNotResetTheStreak`,
  `AccumulatesStreakAndTerminatesAfterMaxStagnantGenerations`).

## Dependencies

- [Models](../Models/API.md) — `Population`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Called once per generation on the orchestrator thread; cheap, no allocation.

## Acceptance criteria

- [x] The three rules pass their unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/TerminationStrategies`, local run
      (part of 115 of 115 unit cases for slice 4).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ The stagnation rule moves the shared population cursor as a side effect.
- [ ] ⚠ The stagnation rule cannot be reused across runs (its streak and last best
      persist) and nothing says so outside this document.
- [ ] ⚠ A `NaN` best fitness counts as stagnation: `|NaN - x| > t` is false (by reading
      the code, not tested).
- [ ] ⚠ No argument checks: a limit of 0 or a negative threshold is accepted.

## Taboos

- **No sharing of a `StagnationStreakTerminationStrategy` instance between runs.** Its
  streak and last best carry over, so the second run starts half-stagnated.
