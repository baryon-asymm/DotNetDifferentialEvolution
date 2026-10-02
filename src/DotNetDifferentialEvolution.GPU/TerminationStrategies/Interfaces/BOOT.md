# BOOT.md — GPU termination contract

## Purpose

When the generation loop stops. Unlike the strategies that run inside the kernel, this
one runs on the host between launches, so it may be a class and may hold state.

## Invariants

- **It is consulted once per generation, after the generation is complete.** Held by
  the shape of `KernelController.Run` ([Kernels](../../Controllers/Kernels/API.md)), a
  `do … while` loop.

## Dependencies

- [Models](../../Models/API.md) — `HostPopulation`.

Outside the tree: ILGPU 1.5.1 (`Accelerator`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Host code. A rule that reads the population pays a device-to-host copy on every
  generation.

## Acceptance criteria

- [x] The run stops through this contract: 2026-10-02, the end-to-end tests in
      `DifferentialEvolutionOptimizerTests` stop at 1 000 generations (local, OpenCL
      `gfx1036`).
- [ ] ⚠ The name `IsMustTerminate` is ungrammatical; renaming it is a public break.

## Taboos

- **No treating `HostPopulation` as host memory.** Despite its name it holds device
  buffers; a rule reads them only through an explicit copy (`GetAsArray1D`,
  `GetAsArray2D`), as the package root does for its result.
