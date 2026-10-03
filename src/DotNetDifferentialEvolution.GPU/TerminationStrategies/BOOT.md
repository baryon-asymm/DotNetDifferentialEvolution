# BOOT.md — GPU TerminationStrategies

## Purpose

The package's one stop rule: a fixed number of generations, evaluated on the host
without touching the device.

## Invariants

- **The rule never reads the device.** It decides from the generation number alone, so
  a run pays no device-to-host copy for it. Held by the code.
- **A run makes exactly `maxGenerationCount` generations when that is ≥ 1.** Follows
  from `generation >= max` and the controller's loop; held by the code and by the
  tests' 1 000-generation runs.

## Dependencies

- [Models](../Models/API.md) — `HostPopulation`, in the signature only.

Outside the tree: ILGPU 1.5.1 (`Accelerator`, in the signature only).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Host code.

## Acceptance criteria

- [x] Ends the end-to-end runs after 1 000 generations: 2026-10-02,
      `DifferentialEvolutionOptimizerTests` (local, OpenCL `gfx1036`).
- [ ] No unit test of the boundary (`max` 0, 1, n).
- [ ] ⚠ A non-positive limit is accepted silently and still runs one generation.
- [ ] ⚠ No other rule exists (fitness target, stagnation, time). The CPU package has
      generation, evaluation and stagnation limits.

## Taboos

- **No device read in a rule that does not need one.** The rule runs every
  generation, so a read adds a device-to-host copy to every generation.
