# BOOT.md — PopulationSamplingMaker

## Purpose

The default start of a CPU run: individuals drawn uniformly in the box, from a provider
the builder can replace with the seeded one so the initial population reproduces too.

## Invariants

- **Finite ordered endpoints give a finite sample in the closed interval.** Draw once per
  gene. Use the usual scaled width when it is finite and interpolate between weighted
  endpoints when it overflows. Limit a rounded overshoot to the upper endpoint.
  ⚠ 2026-11-13: the previous half-open guarantee missed rounding to the upper endpoint
  on adjacent doubles, fixed intervals and width overflow, observed in `SamplesFiniteIntervals`.
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

- [x] 2026-11-13, `UniformRandomSamplingMakerTests.SamplesFiniteIntervals` pins the
      finite-box arithmetic and the draw count. The old sampler failed four cases.

- [x] The sampler passes its unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/PopulationSampling/UniformRandomSamplingMakerTests`,
      local run (part of 115 of 115 unit cases for slice 4).
- [ ] ⚠ No check of the bounds: different lengths or `lower > upper` are accepted, and
      empty bounds divide by zero.
- [ ] ⚠ The directory and namespace are `PopulationSamplingMaker` (singular) while the
      unit tests live in `PopulationSampling`; renaming the namespace is a public break.

## Verification on 2026-11-13

Git tree `7b7dc4e91f02259ad4b3b84afb6e0c23f7d4248a`, after a Release solution build:
`dotnet test tests/DotNetDifferentialEvolution.UnitTests -c Release --no-build --blame-hang-timeout 4m`
passed 264/264, including `UniformRandomSamplingMakerTests.SamplesFiniteIntervals`.
All seven finite-interval sampling rows passed.

## Taboos

- **No sampling from a source the builder cannot replace.** A sampler that ignores
  `UseRandomProvider` makes a seeded run start from a different population every time.
