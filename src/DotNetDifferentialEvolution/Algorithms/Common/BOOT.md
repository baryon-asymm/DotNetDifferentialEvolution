# BOOT.md — Algorithms/Common

## Purpose

The archive maintenance JADE, SHADE and L-SHADE share, and the one source of randomness
this family uses outside the workers (random eviction), made seedable.

## Invariants

- **Only parents beaten strictly enter the archive.** A parent displaced by a tie was
  not beaten (both papers, Algorithm 2 line 16; `ae16907`). Held by
  `JadeStrategyTests.AfterGeneration_IgnoresATrialAcceptedOnATie` and by the code.
- **A non-positive capacity means no archive, never a throw.** A hook may write
  `ArchiveCapacity`; `<= 0` is tested rather than `== 0` (`c54fa57`). Held by
  `JadeStrategyTests.AfterGeneration_WithANegativeArchiveCapacity_LeavesTheArchiveAlone`.
- **Eviction is reproducible in a seeded run.** It draws from the provider the builder
  hands over (`3f3d394`). Held by
  `SeededReproducibilityTests.AnAdaptiveVariantIsReproducibleIncludingItsArchiveEviction`.

## Dependencies

- [GenerationStrategies](../../GenerationStrategies/API.md) — `GenerationContext`.
- [Helpers](../../Helpers/API.md) — `PopulationSortHelper`.
- [Models](../../Models/API.md) — `TrialRecord`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`,
`RandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- Called from the generation hook only, single-threaded.

## Acceptance criteria

- [x] Archive behaviour is covered through the derived strategies' unit tests and the
      seeded adaptive run: 2026-10-02, `JadeStrategyTests`, `ShadeStrategyTests`,
      `SeededReproducibilityTests` (local runs).
- [ ] ⚠ The base class has no tests of its own.
- [ ] ⚠ For an unseeded run the archive draws from the family's default `RandomProvider`
      rather than from a generator derived from the run's root seed, unlike the workers.

## Taboos

- **No archiving on survival.** It would fill the archive with parents that were not
  beaten (`ae16907`).
- **No `== 0` capacity guard.** A negative capacity slipped past it into `Next(capacity)`
  and threw from inside a running generation (`c54fa57`).
