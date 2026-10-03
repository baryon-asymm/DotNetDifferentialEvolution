# BOOT.md — GPU population sampling contract

## Purpose

How the initial population is produced, on the host, before anything reaches the
device. Separate from its implementation so that a caller can seed the search with its
own points.

## Invariants

- **The population size is a property of the sampler.** The controller takes the
  kernel extent from `GetPopulationSize()`, not from the samples. Held by the shape of
  `KernelController` ([Kernels](../../Controllers/Kernels/API.md)).

## Dependencies

None.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Host code; returns `double[,]` because ILGPU's `Allocate2DDenseX` takes that shape
  (CA1814 is off for this package in `.editorconfig` for that reason).

## Acceptance criteria

- [x] The controller allocates the population from this contract and the run
      converges: 2026-10-02, the end-to-end tests in
      `DifferentialEvolutionOptimizerTests` (local, OpenCL `gfx1036`).
- [ ] ⚠ The size is stated twice, by `GetPopulationSize()` and by the rows of
      `TakeSamples()`, and nothing checks that they agree.

## Taboos

- **No device work in a sampler.** It runs before the controller has allocated
  anything and returns host memory.
