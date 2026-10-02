# BOOT.md — UnitTests/MutationStrategies

## Purpose

Level U0 for the p-best scheme of JADE, SHADE and L-SHADE. The pool size is a local
inside `Mutate`; the tests recover it without new API by scripting the p-best draw:
`ScriptedRandomProvider` refuses a value outside `[0, pool)`, so the smallest refused
draw is the pool size (`f7887ab`).

## Invariants

- **The reference is Tanabe's L-SHADE code**: `pNP = max(round(p·N), 2)`, "choose at
  least two best solutions", cited case by case with the arithmetic.
- **The probe population makes the trial identify `x_pbest`**: genome size 1,
  individual `i` holds `i`, F = 0.5, r1 = 1, r2 = 2, so the trial is
  `0.5·x_pbest − 0.5`.
- **The probe scripts exactly the four draws `Mutate` makes**; any other refusal would
  be a different failure, and the comment in the probe says so.

## Dependencies

- [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md)
  — `CurrentToPBestMutationStrategy`, `MutationContext`; under test.
- [Fakes](../../DotNetDifferentialEvolution.Tests.Shared/Fakes/API.md) —
  `ScriptedRandomProvider`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 20 cases in 1 class.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. A floor of 1 instead of
      2 turned 6 cases red, in `Mutate_NeverDrawsPBestFromAPoolSmallerThanTwo` and
      `Mutate_AddressesThePBestPoolThroughTheFitnessRanking`.
- [ ] ⚠ The other six schemes (`Rand`, `Best`, `CurrentToBest`, `RandTwo`, `BestTwo`,
      the legacy `MutationStrategy`) have no unit tests of their own; their arithmetic
      is covered through [Helpers](Helpers/BOOT.md), their wiring by the integration
      convergence tests.
- [ ] ⚠ The SHADE per-individual p range (`min(2/N, p)` to `p`) is checked only for
      its constructor, not for the pool sizes it produces.

## Taboos

- **No public accessor for the pool size.** The scripted-draw probe reads it through
  the code path the engine uses.
