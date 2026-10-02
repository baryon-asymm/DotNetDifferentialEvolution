# BOOT.md — termination contract

## Purpose

When a CPU run stops, as one question asked of the population. Separate from the
built-in rules so a consumer can supply their own.

## Invariants

- **The rule sees the population with its counters stamped for the moment of the
  call** (`GetRepresentativePopulation`). Held by the `Models` tests of that method.

## Dependencies

- [Models](../../Models/API.md) — `Population`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition: none.

## Acceptance criteria

- [x] The built-in rules implement it and stop runs in the unit and integration suites:
      2026-10-02, local run (part of 115 of 115 unit cases for slice 4).
- [ ] ⚠ Part of the `Models` cycle: the contract takes a `Population`, and
      `ProblemContext` holds an `ITerminationStrategy`.
- [ ] ⚠ A rule may be stateful (the stagnation rule is), but the contract says nothing
      about reuse across runs.

## Taboos

- **No writes to the population a rule is given.** It is the engine's live object, not
  a copy, and its `GenerationNumber`, `BestIndividualIndex` and `EvaluationCount` are
  publicly settable; a rule that sets them changes what observers and the result
  report.
