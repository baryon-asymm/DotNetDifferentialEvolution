# BOOT.md — GenerationStrategies

## Purpose

Where self-adaptive variants learn: after each generation a hook sees what every trial
did and updates its parameter memory, maintains the external archive and, for L-SHADE,
shrinks the population. The node defines that hook and the deliberately narrow view of
the run it is given.

## Invariants

- **The hook runs single-threaded, between generations, after the swap.** No worker is
  running, so it needs no synchronisation. Held by the engine's orchestrator; stated by
  the contract.
- **The hook cannot reach the run's control state.** `GenerationContext` exposes no
  swap, no best index, no stop rule, no evaluator, and the evaluation count read-only
  (`293b2b1`). Held by the shape of the class (sealed, wrapping a private context).
- **Narrowing the active size narrows both populations.** It goes through
  `ProblemContext.CurrentPopulationSize`. Held by the `Models` tests of that setter.

## Dependencies

- [Models](../Models/API.md) — `ProblemContext` (wrapped), `PopulationView`,
  `TrialRecord`.
- [MutationStrategies/Interfaces](../MutationStrategies/Interfaces/API.md) —
  `MutationRequirements`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `GenerationContext`'s constructor stays public so a hook can be tested against a
  context built by hand, as the built-in variants' tests do.

## Acceptance criteria

- [x] The built-in hooks run through this contract in the variants' unit and
      integration tests: 2026-10-02, local run of the CI filters (232 unit,
      70 integration).
- [ ] `GenerationContext` has no test of its own (e.g. that it exposes no way to swap).
- [ ] ⚠ Part of the `Models` cycle: `GenerationContext` wraps `ProblemContext`, which
      holds an `IGenerationStrategy`.

## Taboos

- **No `ProblemContext` handed to a hook.** A third-party hook in the middle of a run
  could swap the populations, rewrite the best index and the evaluation counter or
  replace the archive buffer (`293b2b1`).
- **No settable evaluation count for the hook.** L-SHADE schedules against the budget
  but has no business moving the counter (`293b2b1`).
