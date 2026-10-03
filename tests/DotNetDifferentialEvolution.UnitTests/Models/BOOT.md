# BOOT.md — UnitTests/Models

## Purpose

Level U0 for the package's state types. The recurring subject is the gap between
allocated and live: L-SHADE shrinks the population inside buffers that keep their full
size, and a consumer that read the allocated length saw individuals dropped
generations earlier (TD-3, named in `PopulationViewTests`). These tests hold every
consumer-facing size to the live count.

## Invariants

- **Shrinking is tested on both buffers and across a swap.** The current and trial
  buffers trade places every generation, so narrowing only one would undo itself at the
  next swap.
- **The stale-individual case is pinned by index**: after shrinking to one, indices 1
  and 2 are allocated but refused.

## Dependencies

- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — under test.
- [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — the limit the contexts are built with.
- [TestSupport](../TestSupport/API.md) — `PopulationFactory`.
- [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md),
  [Helpers](../../DotNetDifferentialEvolution.Tests.Common/Helpers/API.md) — Sphere,
  `ProblemContextHelper`.
- [GenerationStrategies](../../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md) — `IGenerationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md) — `ITerminationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [FitnessFunctionEvaluators/Interfaces](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/Interfaces/API.md) — `ITestFitnessFunctionEvaluator`. Added 2026-10-03 from the reflection check (`DependencyTests`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 20 cases in 4 classes (cursor 3, population 8, view 6,
      context 3).
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Bounding `MoveCursorTo`
      by `Capacity` instead of the live `PopulationSize` turned
      `MoveCursorToRefusesAnIndexOutsideTheActivePopulation` red.
- [ ] ⚠ The contexts come from `ProblemContextHelper` without a seed, so their
      populations are random; no assertion here depends on the values.

## Taboos

- **No size read from a buffer's length** where the live count is meant.
