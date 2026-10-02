# BOOT.md — GPU test objectives

## Purpose

The references of the GPU end-to-end tests: two objectives whose optimum is known
independently of the optimizer, each compiled into the kernel like a user's objective
and each carrying its expected answer next to its formula.

## Invariants

- **Each expected answer is an independent truth, not a recorded run.**
  - Rosenbrock: the analytic minimum, 0 at `(1, 1)`.
  - Polynomial: the exact least-squares solution. Checked 2026-10-02 with NumPy
    `linalg.lstsq` on the same 12 points: the stored coefficients differ from it by at
    most 2.15e-9 and the stored fitness by 2.2e-18; the design matrix has condition
    number 5.07e5. The test's tolerance is 1e-8, so a result must land within about
    7.8e-9 of the exact optimum to pass. A one-off check, not a test in the repository.
- **The objectives write exactly their own fitness slot.** As the package's objective
  contract requires ([Interfaces](../../../src/DotNetDifferentialEvolution.GPU/Interfaces/API.md)).

## Dependencies

- [Interfaces](../../../src/DotNetDifferentialEvolution.GPU/Interfaces/API.md) —
  `IFitnessFunctionInvoker`.
- [Models](../../../src/DotNetDifferentialEvolution.GPU/Models/API.md) —
  `DevicePopulation`.

Outside the tree: ILGPU.Algorithms 1.5.1 (`XMath.Pow` in the polynomial).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Kernel code. The polynomial keeps its 12 data points as array literals inside
  `Invoke`; ILGPU accepts them (the tests pass on OpenCL), but they are rebuilt per call.

## Acceptance criteria

- [x] Both objectives compile into the kernels and the run converges to their stored
      answers: 2026-10-02, `DifferentialEvolutionOptimizerTests`, 5 of 5 runs (local,
      OpenCL `gfx1036`).
- [x] The polynomial's stored answer is the exact least-squares optimum: 2026-10-02,
      NumPy `lstsq` (see Invariants).
- [ ] ⚠ The expected answers live as properties of the objectives, in code; the
      polynomial's are 17-digit literals with no record of how they were obtained
      until this document.
- [ ] ⚠ Rosenbrock is 2-D only; no objective tests a dimension above 6.

## Taboos

- **No expected answer copied from an optimizer run.** A run's output as reference
  makes the test check that the optimizer agrees with itself.
- **No change of the polynomial's data points without recomputing the exact
  optimum.** The stored coefficients are tied to these 12 points.
