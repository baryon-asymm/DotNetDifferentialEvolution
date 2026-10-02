# BOOT.md — GPU Models

## Purpose

The data shapes of the GPU package. `DevicePopulation` is what a kernel sees: two
`ArrayView`s, one fitness value and one gene vector per individual. `HostPopulation` is
the host-side handle that owns nothing but holds the two device buffers behind a view.
`OptimizationResult` and `Individual` are host snapshots returned to the caller. The
node is separate because every strategy, interface and the controller exchange these
types and none of them should own their definition.

## Invariants

- **`DevicePopulation` is a value type holding only `ArrayView`s and computing its sizes
  from them.** That is what lets ILGPU pass it into a kernel by value. Held by the shape
  of the code and by the kernels compiling at run time (any reference-type field would
  fail `LoadAutoGroupedStreamKernel`); there is no compile-time check.
- **The gene matrix is `Stride2D.DenseX`, individuals along X.** `PopulationSize` is
  `Extent.X`, `VectorSize` is `Extent.Y`. Every kernel indexes `Individuals[individual,
  gene]`; changing the layout silently transposes every strategy. Held by the shape of
  the code only.
- **Lower fitness is better.** Not stated in these types, but every consumer of
  `FitnessFunctionValues` compares with `<`; documented here because this is where the
  values live.
- **Result types copy their input.** `OptimizationResult` and `Individual` call
  `ToArray()` on the sequence they get, so a later change of the source does not leak
  into the result. Held by the shape of the code.

## Dependencies

None.

Outside the tree: ILGPU 1.5.1 (`ArrayView1D`, `ArrayView2D`, `MemoryBuffer1D`,
`MemoryBuffer2D`, `Stride1D.Dense`, `Stride2D.DenseX`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `DevicePopulation` must stay usable inside an ILGPU kernel: no reference-type fields,
  no virtual members, no allocation.
- Built under the repository's analyzer policy; CA1815 (equality on value types) is off
  for this package in `.editorconfig` because kernel structs are never compared.

## Acceptance criteria

- [x] A population flows through `DevicePopulation` into the kernels and back to the
      host intact enough to converge: 2026-10-02,
      `DifferentialEvolutionOptimizerTests.TestRosenbrockCase` and
      `TestPolynomialApproximationFunctionCase` (local, OpenCL `gfx1036`).
- [ ] No unit test checks `PopulationSize`/`VectorSize` against the buffers' extents.
- [ ] ⚠ `Individual` is public but used by no public member: only
      `DifferentialEvolutionOptimizer` uses it, internally, on the way to
      `OptimizationResult`. Two public types carry the same data.
- [ ] ⚠ `HostPopulation` accepts buffers of different population sizes without a
      check; the mismatch shows up later as out-of-range reads in a kernel.

## Taboos

- **No reference-type or virtual members in `DevicePopulation`.** ILGPU cannot pass it
  into a kernel then, and the failure appears only when the kernel is loaded at run
  time.
- **No change of the 2D stride or of the meaning of X/Y.** Every strategy indexes
  `[individual, gene]` by convention; a layout change compiles and runs and computes
  garbage.
- **No disposal of buffers in `HostPopulation`.** The controller owns the buffers and
  disposes them itself; a second owner would double-free.
