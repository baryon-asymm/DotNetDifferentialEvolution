# BOOT.md — UnitTests/ControlParameterProviders

## Purpose

Level U0 for the classic-DE providers of F and CR: the exact value for a scripted draw,
and the number of draws taken.

## Invariants

- **Draw counts are asserted, not only values.** The constant provider is held to zero
  draws, so a provider that consumed randomness it does not need would shift every
  later draw of a seeded run.
- **The dithered mapping is checked at both ends and the midpoint** (draws 0, 0.5, 1).

## Dependencies

- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md)
  — under test.
- [Fakes](../../DotNetDifferentialEvolution.Tests.Shared/Fakes/API.md) —
  `ScriptedRandomProvider`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 4 cases in 2 classes.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Halving the dithered
      range turned `SamplesMutationForceWithinRangeFromTheRandomDraw` red.
- [ ] ⚠ The draw 1.0 is scripted although `NextDouble` never returns it; it checks
      the formula's upper end, not a reachable case.

## Taboos

- **No assertion on values alone where the draw count matters.**
