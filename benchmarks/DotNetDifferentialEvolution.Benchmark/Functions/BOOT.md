# BOOT.md — Benchmark/Functions

## Purpose

The two multimodal objectives the convergence comparison runs every variant on.

## Invariants

- **Pure**: no state, so the worker overload simply delegates.
- **The formulas match the shared library's Rastrigin and Ackley**
  ([FitnessFunctionEvaluators](../../../tests/DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/API.md)),
  read side by side on 2026-10-02 (slice 6).

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Builds with the project, 0 warnings: 2026-10-02.
- [ ] ⚠ These are second copies of functions the shared library already has, with
      bounds and declared optima, and the project already references it. The shared
      library's taboo on second copies
      ([Tests.Shared](../../../tests/DotNetDifferentialEvolution.Tests.Shared/BOOT.md))
      is broken here; nothing checks that the copies stay equal.
- [ ] ⚠ No test evaluates them.
- [ ] ⚠ Each file imports `DotNetDifferentialEvolution.Interfaces` and uses nothing
      from it.

## Taboos

- **No change to one copy without the other**, while both exist.
