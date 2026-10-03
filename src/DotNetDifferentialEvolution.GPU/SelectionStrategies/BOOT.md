# BOOT.md — GPU SelectionStrategies

## Purpose

The default survival rule of the GPU package: classic DE greedy selection, executed by
the thread that owns the index.

## Invariants

- **`next[index]` is always written in full**, genes and fitness, from exactly one
  source. Held by the shape of the code (both branches copy the whole vector and the
  fitness).
- **The comparison is strict `<`: ties keep the parent.** Held by the code; no test.

## Dependencies

- [Models](../Models/API.md) — `DevicePopulation`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Kernel code: a stateless `readonly struct`, compiled by ILGPU.

## Acceptance criteria

- [x] With this rule the run converges on Rosenbrock (2-D) to fitness 0 ± 1e-6 and on a
      6-coefficient polynomial fit to the expected value ± 1e-8: 2026-10-02,
      `DifferentialEvolutionOptimizerTests` (local, OpenCL `gfx1036`).
- [ ] No unit test of ties or `NaN`.
- [ ] ⚠ Ties keep the parent. The CPU package changed the same rule in its 5.x line so
      that a tie lets the trial survive (its commit `ae16907`, "survival takes ties");
      the two packages now differ on plateaus.
- [ ] ⚠ A `NaN` parent is never replaced: an individual whose fitness became `NaN`
      stays in the population for the rest of the run.

## Taboos

- **No partial copy of an individual.** The next population's buffer still holds the
  population from two generations back (the controller swaps buffers); whatever is not
  written is stale.
