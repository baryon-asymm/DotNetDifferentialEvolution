# BOOT.md — DotNetDifferentialEvolution.GPU

## Purpose

Differential Evolution with the whole population on a GPU, through ILGPU. Each
generation is one kernel launch in which every thread builds, evaluates and selects one
individual; the objective is user code compiled into that kernel. The package exists
for objectives cheap enough per call and populations large enough (the tests use 10 000)
that one GPU thread per individual beats CPU threads.

Published on nuget.org as `DotNetDifferentialEvolution.GPU` 0.1.0, 0.0.2 and 0.2.0
(2024-08); developed in its own repository until it was imported here on 2026-10-02
(merge `fe13623`). The csproj still says 0.0.2.

Not goals, as the code stands: a builder, seeding, adaptive variants, host-side
objectives, the shared `DotNetOptimization.Abstractions` contracts.

## Invariants

- **Every strategy and the objective are generic struct parameters of the
  controller.** ILGPU specialises the kernels per combination, with no virtual calls on
  the device. Held by the generic constraints in
  [Controllers/Kernels](Controllers/Kernels/API.md).
- **The population stays on the device for the whole run.** It reaches the host only
  when the stop rule or the observer copies it, and once at the end for the result.
  Held by the shape of the controller and the optimizer.
- **Per generation, thread `index` writes only trial `index` and next `index`.** That
  is what makes a generation race-free without synchronisation inside the launch. Held
  by the contracts of the strategy nodes, not by a check.
- **Lower fitness is better, comparisons are strict `<`.** Held by the code of
  selection and of the optimizer's best-pick.
- **The package depends on nothing in this repository.** No reference to the CPU
  package or to `DotNetOptimization.Abstractions`. Held by the csproj.

## Dependencies

None.

Outside the tree: ILGPU 1.5.1 and ILGPU.Algorithms 1.5.1; .NET 8. The tests need a
device ILGPU can open through OpenCL.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Kernel code (the objective, mutation, selection, random generator, device population)
  must compile under ILGPU: value types, no virtual calls, no managed allocation beyond
  small fixed local arrays, no exceptions. Failures appear at run time, when the
  optimizer's constructor loads the kernels.
- Two analyzer rules are off for this package and its tests in `.editorconfig`, with
  reasons: CA1815 (kernel structs are never compared) and CA1814 (ILGPU's 2D
  allocation takes `double[,]`). Everything else of the repository policy applies.
- The package ships `README.md` and `ILGPU_LICENSE` from this directory and the
  repository's `LICENSE`.

## Acceptance criteria

- [x] The package builds under the repository's analyzer policy with 0 warnings:
      2026-10-02, `dotnet build DotNetDifferentialEvolution.sln -c Release`.
- [x] Two end-to-end runs converge (Rosenbrock 2-D; 6-coefficient polynomial fit):
      2026-10-02, `DifferentialEvolutionOptimizerTests`, 2 of 2 (local, OpenCL
      `gfx1036`).
- [x] `dotnet pack` produces a package with the DLL, `README.md`, `LICENSE` and
      `ILGPU_LICENSE`: 2026-10-02, local pack into the session scratchpad.
- [ ] No test runs in CI: hosted runners have no OpenCL device (root `BOOT.md`).
- [ ] ⚠ `RunAsync` is synchronous: it blocks the caller and returns a completed task.
- [ ] ⚠ If individual 0's fitness is `NaN`, the result is `NaN`.
- [ ] ⚠ The constructor compiles kernels and allocates device memory; constructing an
      optimizer is the expensive and failing step, not running it.
- [ ] ⚠ `Dispose` forces `GC.Collect()`.
- [ ] ⚠ Diverges from the CPU package in semantics a user may carry over: ties keep the
      parent, no `jrand`, out-of-box genes re-drawn, cancellation not reported as such,
      no seed.
- [ ] ⚠ `README.md` predates the import: its licence, ILGPU-licence and issue links
      point to the old repository, and it says the library "automatically detects the
      suitable device (GPU or CPU)" while its own example opens an OpenCL-only context.

## Taboos

- **No reference to ILGPU from the CPU package, and none from here to the CPU
  package.** The root's taboo; the two packages stay siblings.
- **No host round trip per generation in the core loop.** The random-number design
  was rebuilt on 2024-08-10 (`1ad5a86`) to remove one.
- **No `GeneratePackageOnBuild`.** Removed at the import (`54cf002`); packages are
  packed from a tagged commit (root `BOOT.md`).

## Decomposition

One directory per role (controller and kernels, mutation, selection, random generator,
initial sampling, termination, models), plus the package-level `Interfaces`. Every
strategy directory has an `Interfaces` subdirectory holding its one contract (the
controller's is `Controllers/Kernels/Interfaces`; `Models` has none); the list is in
[API.md](API.md), `## Children`. No cycles between
them (textual estimate, 2026-10-02).

The `*/Interfaces` directories are nodes of their own, each with a single interface.
Owner's decision, 2026-10-02: describe them as they are; merging each into its parent
would break the public namespaces, and is left for the GPU redesign.
