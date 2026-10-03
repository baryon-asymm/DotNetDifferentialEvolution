# BOOT.md — DotNetDifferentialEvolution.GPU/Objectives

## Purpose

The contract between the caller's objective and the kernel: the interface a struct
implements, and the read-only view of one individual it is given. Its own node because
it is the only public namespace of the package besides the root, and the one a caller
writes code against.

## Invariants

- **The objective cannot write the population.** `GeneView` has no setter, no member
  returning by reference and no public field; that is half of what keeps a generation
  race-free (root `BOOT.md`, invariant 2). Held by ACCEPTANCE.md, check 2a.
- **The objective is a struct type argument**, never an interface reference: the kernel
  is compiled for it and calls it without virtual dispatch. Held by the
  `struct, IGpuFitnessFunction` constraints of the builder and the kernels.
- **No bounds check in the view.** Kernels cannot throw; an index outside
  `0 ≤ j < Length` is undefined. Held by the code, stated in `API.md`.

## Dependencies

None.

Outside the tree: ILGPU 1.5.3 (`ArrayView<double>`, `MemoryBuffer`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `GeneView` stays a plain value type with one `ArrayView<double>` field: it is built
  inside the kernel for every evaluation.
- Its equality members read the view's buffer and offset through constrained generic
  calls, so they never box; they are host-only and no kernel reaches them.

## Acceptance criteria

- [x] Builds under the maximum diagnostics with 0 warnings: 2026-10-03, `dotnet build`
      of the package.
- [x] An objective written against this contract runs on the CPU accelerator, CUDA and
      OpenCL: 2026-10-03, a Sphere 5-D smoke run (N 50, 300 generations, seed 1)
      reached 1.68e-27 on the CPU accelerator and OpenCL (`gfx1036`), 1.74e-27 on CUDA
      (RTX 5070 Ti); one run each.
- [ ] Check 2a (no writable member) — see the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Taboos

- **No writable member on `GeneView`**, not even an internal one a kernel could reach.
- **No interface-typed objective anywhere on the device path.**
