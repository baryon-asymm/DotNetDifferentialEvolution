# BOOT.md — GPU Kernels

## Purpose

The engine of the GPU package. It turns four strategy structs into two ILGPU kernels
(init and run), owns the device populations, and runs one kernel launch per
generation, with the stop rule and the observer on the host in between.

## Invariants

- **One generation is one kernel launch, one thread per individual, then
  `device.Synchronize()`.** Mutation, evaluation and selection of individual `index`
  happen in that thread in that order. Held by the shape of the code.
- **No thread writes what another thread reads within a launch.** The run kernel reads
  `current` (any index), writes `trial[index]` and `next[index]` only. Held by the
  contracts of the strategies and the objective, not by any check.
- **Double buffering by swap.** After each generation `current` and `next` swap
  references; nothing is copied. The trial buffer is reused every generation.
- **The strategies are generic struct parameters.** ILGPU specialises the kernels for
  them; there is no virtual dispatch on the device. Held by the generic constraints.
- **Cancellation is cooperative and ends normally.** The token is read after each
  generation; `Run` returns, it does not throw.

## Dependencies

- [Models](../../Models/API.md) — `DevicePopulation`, `HostPopulation`.
- [Interfaces](../../Interfaces/API.md) — `IFitnessFunctionInvoker`,
  `IUpdateOptimizerHandler`, `OptimizerState`.
- [MutationStrategies/Interfaces](../../MutationStrategies/Interfaces/API.md) —
  `IMutationStrategy<T>`.
- [PopulationSamplingMakers/Interfaces](../../PopulationSamplingMakers/Interfaces/API.md)
  — `IPopulationSamplingMaker`.
- [RandomGenerators/Interfaces](../../RandomGenerators/Interfaces/API.md) —
  `IRandomGenerator`.
- [SelectionStrategies/Interfaces](../../SelectionStrategies/Interfaces/API.md) —
  `ISelectionStrategy`.
- [TerminationStrategies/Interfaces](../../TerminationStrategies/Interfaces/API.md) —
  `ITerminationStrategy`.

Outside the tree: ILGPU 1.5.1 (`Context`, `Accelerator`, `Index1D`,
`LoadAutoGroupedStreamKernel`, `Allocate1D`, `Allocate2DDenseX`).

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- Kernel bodies are static methods taking only value-type parameters.
- The host loop is synchronous: the calling thread blocks for the whole run.

## Acceptance criteria

- [x] Two end-to-end runs converge (population 10 000, 1 000 generations): Rosenbrock
      (2-D) to 0 ± 1e-6 at (1, 1) ± 1e-6, and a 6-coefficient polynomial fit to the
      expected fitness and coefficients ± 1e-8: 2026-10-02,
      `DifferentialEvolutionOptimizerTests.TestRosenbrockCase` and
      `TestPolynomialApproximationFunctionCase` (local, OpenCL `gfx1036`, 2 s and 6 s,
      n = 1 run each).
- [ ] No test of the lifecycle errors, of cancellation, of the observer's calls, or of
      a second `Run`.
- [ ] ⚠ `Run` does not require `Init`: without it the current fitness is all zeros and
      the search stalls unless the objective goes negative.
- [ ] ⚠ `Dispose` disposes the caller's `Context` and `Accelerator` while leaving the
      caller's bound and random-state buffers alone: ownership is half taken.
- [ ] ⚠ Nothing checks that the random-state buffer and the bound buffers match the
      population and vector sizes.
- [ ] ⚠ The three populations start as copies of the same samples, so `next` and
      `trial` hold meaningful-looking data before they are ever written.
- [ ] ⚠ Cancellation returns a normal result; the CPU package completes the task as
      cancelled instead.

## Taboos

- **No host round trip inside the generation loop beyond what the stop rule and the
  observer ask for.** The loop exists to keep the population on the device; the
  random-number design was changed for the same reason (`1ad5a86`, 2024-08-10).
- **No write by a thread to an index other than its own.** Threads of a launch run in
  any order.
