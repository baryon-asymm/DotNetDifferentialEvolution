# BOOT.md — PopulationSamplingMaker

## Purpose

The default start of a CPU run: individuals drawn uniformly in the box, from a provider
the builder can replace with the seeded one so the initial population reproduces too.

## Invariants

- **Every gene lies in its own dimension's `[lower, upper)`.** Held by
  `UniformRandomSamplingMakerTests.SamplesEveryGeneWithinItsPerDimensionBounds`.
- **The whole buffer is filled**, every individual of the capacity. Held by
  `UniformRandomSamplingMakerTests.FillsTheEntireBuffer`.
- **A seeded run reaches the sampler through `UseRandomProvider`** (`3f3d394`). Held by
  `SeededReproducibilityTests.TheSameSeedReproducesTheInitialPopulationToo`
  (integration).

## Dependencies

- [Interfaces](../Interfaces/API.md) — `IPopulationSamplingMaker`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`,
`RandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Called once per run, before the workers start.

## Acceptance criteria

- [x] The sampler passes its unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/PopulationSampling/UniformRandomSamplingMakerTests`,
      local run (part of 115 of 115 unit cases for slice 4).
- [ ] ⚠ No check of the bounds: different lengths or `lower > upper` are accepted, and
      empty bounds divide by zero.
- [ ] ⚠ The directory and namespace are `PopulationSamplingMaker` (singular) while the
      unit tests live in `PopulationSampling`; renaming the namespace is a public break.

## Taboos

- **No sampling from a source the builder cannot replace.** A sampler that ignores
  `UseRandomProvider` makes a seeded run start from a different population every time.
