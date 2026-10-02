# BOOT.md — GPU Interfaces

## Purpose

The contracts on the package's outer edge: what the caller runs
(`IDifferentialEvolutionOptimizer<T>`), what the caller writes so that it runs on the
GPU (`IFitnessFunctionInvoker`), and what the caller may plug in to watch a run
(`IUpdateOptimizerHandler` with `OptimizerState`). Separate from the strategy
interfaces, which configure the search; these frame the user's own code.

## Invariants

- **The objective is evaluated on the device, one thread per individual, writing only
  its own fitness slot.** `IFitnessFunctionInvoker.Invoke(individualIndex,
  population)` is the whole contract; the kernels call it with the thread index.
  Held by the shape of the kernels in [Kernels](../Controllers/Kernels/API.md) and by
  the end-to-end tests.
- **Lower fitness is better.** Every consumer compares with `<`.
- **The observer runs on the host, synchronously, between generations.** It is called
  after `device.Synchronize()`, so the population it receives is complete. Held by the
  shape of `KernelController.Run`.

## Dependencies

- [Models](../Models/API.md) — `DevicePopulation` (objective) and `HostPopulation`
  (observer).

Outside the tree: ILGPU 1.5.1 (`Accelerator` in the observer's signature).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- An implementation of `IFitnessFunctionInvoker` is kernel code: it must be a struct
  and compile under ILGPU (see `API.md`). The interface cannot express this; the
  kernel controller's generic constraint `struct, IFitnessFunctionInvoker` enforces
  the struct part, ILGPU the rest, at kernel load time.

## Acceptance criteria

- [x] A struct objective implementing `IFitnessFunctionInvoker` runs on the device and
      drives convergence: 2026-10-02, `RosenbrockFunction` and
      `PolynomialApproximationFunction` in `DifferentialEvolutionOptimizerTests`
      (local, OpenCL `gfx1036`).
- [ ] No test covers `IUpdateOptimizerHandler`: neither the order of states nor the
      generation numbers are checked.
- [ ] ⚠ The objective writes its result into the population instead of returning it.
      An implementation that forgets the write, or writes another index, is not
      detected: selection then compares a stale value.
- [ ] ⚠ `IDifferentialEvolutionOptimizer<T>.RunAsync` suggests asynchrony that the
      package's implementation does not provide (it blocks the caller).
- [ ] ⚠ `OptimizerState` and `IUpdateOptimizerHandler` share one file,
      `IUpdateOptimizerHandler.cs`.

## Taboos

- **No host-side work inside `IFitnessFunctionInvoker`.** It runs on the GPU; anything
  ILGPU cannot compile fails only at kernel load time, in the user's process.
- **No dependence on call order between individuals.** Threads of one launch run in
  any order; an objective reading another individual's slot reads a race.
