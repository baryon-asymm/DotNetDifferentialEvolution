# BOOT.md — UnitTests/PopulationSampling

## Purpose

Level U0 for `UniformRandomSamplingMaker`: structural properties only. The maker draws
from its default provider, so values are not asserted; per-dimension bounds and full
coverage are.

## Invariants

- **The bounds differ per dimension** (`[-5, 5]`, `[10, 20]`, `[0, 1]`), so a maker
  that used one dimension's bounds for all would fail.
- **Unwritten slots are detectable**: the buffer is pre-filled with `NaN`.

## Dependencies

- [PopulationSamplingMaker](../../../src/DotNetDifferentialEvolution/PopulationSamplingMaker/API.md)
  — under test.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 2 cases in 1 class.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Scaling the draw by the
      upper bound instead of the width turned
      `SamplesEveryGeneWithinItsPerDimensionBounds` red.
- [ ] ⚠ Unseeded: the tests use the default provider (`Random.Shared`), so a failure
      depends on the draw. The assertions hold for any draw of a correct maker.
- [ ] ⚠ Uniformity is not checked, and neither is seeding through
      `UseRandomProvider` (the builder's path); seeded reproducibility of the initial
      population is an integration test.

## Taboos

- **No assertion on sampled values** without a seeded provider.
