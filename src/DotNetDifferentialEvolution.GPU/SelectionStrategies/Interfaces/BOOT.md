# BOOT.md — GPU selection contract

## Purpose

The one-to-one survival rule of DE, as kernel code: thread `index` compares its parent
with its trial and writes the survivor into the next population. Separate from the
implementation so the controller is generic over the rule.

## Invariants

- **Selection is per index: thread `index` reads index `index` of the current and trial
  populations and writes index `index` of the next.** That is what makes a generation
  one kernel launch with no synchronisation between threads. Held by the shape of the
  run kernel ([Kernels](../../Controllers/Kernels/API.md)).

## Dependencies

- [Models](../../Models/API.md) — `DevicePopulation`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Implementations are kernel structs that ILGPU can compile.

## Acceptance criteria

- [x] Used by the run kernel through this contract, and the run converges: 2026-10-02,
      the end-to-end tests in `DifferentialEvolutionOptimizerTests` (local, OpenCL
      `gfx1036`).

## Taboos

- **No read or write of another index.** Other threads of the same launch are writing
  them; the result would depend on scheduling.
