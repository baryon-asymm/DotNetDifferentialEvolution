# BOOT.md — GPU test helpers

## Purpose

Ready-made device populations for tests that exercise one strategy in isolation. As
the code stands nothing uses them: no test of a single strategy exists.

## Invariants

- **The populations use the package's layout**: genes in a `DenseX` 2D buffer,
  fitness in a dense 1D buffer, wrapped in `DevicePopulation`. Held by the shape of the
  code.

## Dependencies

- [Models](../../../src/DotNetDifferentialEvolution.GPU/Models/API.md) —
  `DevicePopulation`.

Outside the tree: ILGPU 1.5.1 (`Accelerator`, `Allocate1D`, `Allocate2DDenseX`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Host code that allocates on the device.

## Acceptance criteria

- [ ] ⚠ Dead code: no test references `PopulationHelper` (checked 2026-10-02 by
      searching the test project).
- [ ] ⚠ Each call drops two `MemoryBuffer` objects undisposed and returns only views
      over them; nothing in the test project can free that memory before the
      accelerator goes (whether the accelerator frees it then was not verified).

## Taboos

- **No use of these helpers in a loop as they are.** Every call allocates device memory
  that nothing in the test can release.
