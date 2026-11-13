# BOOT.md — UnitTests/PopulationSampling

## Purpose

Level U0 for `UniformRandomSamplingMaker`. The default provider exercises per-dimension
bounds and buffer coverage. Scripted draws pin arithmetic and draw count.

## Invariants

- **The bounds differ per dimension** (`[-5, 5]`, `[10, 20]`, `[0, 1]`), so a maker
  that used one dimension's bounds for all would fail.
- **Unwritten slots are detectable**: the buffer is pre-filled with `NaN`.

## Dependencies

- [PopulationSamplingMaker](../../../src/DotNetDifferentialEvolution/PopulationSamplingMaker/API.md)
  — under test.
- [Fakes](../../DotNetDifferentialEvolution.Tests.Common/Fakes/API.md) — `ScriptedRandomProvider`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 2 cases in 1 class.
- [x] 2026-11-13, `SamplesFiniteIntervals` checks wide, fixed, adjacent and ordinary
      intervals, one draw per gene, and a scripted endpoint whose rounded sum overshoots.
      The original expression failed the three overflowing-width cases and the overshoot.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Scaling the draw by the
      upper bound instead of the width turned
      `SamplesEveryGeneWithinItsPerDimensionBounds` red.
- [ ] ⚠ The two structural tests use the default provider (`Random.Shared`), so a failure
      depends on the draw. Their assertions hold for any draw of a correct maker.
      ⚠ 2026-11-13. Previous criterion: “⚠ Unseeded: the tests use the default provider
      (`Random.Shared`), so a failure depends on the draw. The assertions hold for any
      draw of a correct maker.” `SamplesFiniteIntervals` now scripts its draws. The
      original two structural tests still use the default provider.
- [ ] ⚠ Uniformity is not checked, and neither is seeding through
      `UseRandomProvider` (the builder's path); seeded reproducibility of the initial
      population is an integration test.

## Taboos

- **Use a seeded or scripted provider for exact-value assertions.**
