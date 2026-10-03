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

## Redesign proposals ⏳

Design mode, 2026-10-03: proposals only, no code. Nothing below is decided until the
owner says so. "Agreed in principle" means the owner agreed on 2026-10-02 when the import
was planned; "open" means it needs an answer. Each item says what it would change and
what this document recommends.

1. **Result contract**: agreed in principle. The optimizer returns
   `DotNetOptimization.Abstractions`' solution type, as the CPU package does, and the
   package takes that dependency. It still references nothing of the CPU package, so
   the root taboo holds.
2. **A fluent builder** shaped like the CPU one: agreed in principle. It replaces
   constructing `KernelController<…>` with four generic struct arguments by hand. The
   struct generics stay inside, because they are what keeps virtual calls off the device.
3. **Classic DE aligned with `docs/ALGORITHMS.md`**: agreed in principle. That means
   `jrand` (at least one gene from the mutant), ties keep the trial, and a seed. Open:
   the bound rule. Today an out-of-box gene is re-drawn; the CPU package repairs.
   Recommendation: the CPU's rule, so one seed-free description fits both packages.
4. **v1 scope**: open. Recommendation: classic DE only in the first new release, jDE in
   the next minor. Items 1–3 already make it a breaking release. jDE needs per-individual
   F and CR buffers on the device and their update between launches, a design of its
   own.
5. **Precision**: open. Recommendation: keep `double` for v1 (CPU parity, comparable
   results). Measure `float` on both reference devices (`gfx1036` over OpenCL, RTX
   5070 Ti over CUDA) before offering it. Consumer GPUs run FP64 at a fraction of the
   FP32 rate, so the gain may be large, but nobody has measured it: not here, and not in
   the two ILGPU projects on the same machine. Both use only `double`, for physics
   reasons; CPM withdrew its "FP64 at 1/64 on GeForce" figure as unmeasured (CPM
   `HISTORY.md`, 2026-09-27).
6. **Objective interface**: open. Today `IFitnessFunctionInvoker.Invoke` writes into the
   population itself, so a buggy objective can write another individual's slot.
   Recommendation: a struct method that takes one individual's genes and returns the
   value, which the kernel writes. Then the race-freedom invariant is held by the
   kernel, not by every user.
7. **Release path and version**: open. Recommendation: a separate tag prefix (for
   example `gpu-v*`) with its own job in `release.yml`, and a first version above 0.2.0
   (1.0.0, since items 1–6 break the API). That clears the two ⚠ items of the root
   `BOOT.md`. Then archive the old repository with a pointer here, since nuget.org's
   project URLs lead to it, and fix `README.md`'s links.
8. **Known defects to fix in the same release**, all recorded above under acceptance
   criteria:
   - `RunAsync` is synchronous;
   - a `NaN` at individual 0 poisons the result;
   - `Dispose` calls `GC.Collect()`;
   - cancellation is not reported as such;
   - the `*/Interfaces` subnodes stay separate. Merging them into their parents changes
     public namespaces, which only a breaking release may do (owner's decision of
     2026-10-02, under `## Decomposition`).

Items 9–12 were added on 2026-10-03. They come from what two other ILGPU projects on the
owner's machine established: APT (`C:\Projects\AerospacePropellantThermodynamics`) and
CPM (`C:\Projects\CompositePropellantMicrostructure`). Both run CUDA plus ILGPU's CPU
accelerator, and neither uses OpenCL. Their paths are cited from their own documents,
read 2026-10-03, and not re-measured here.

9. **CUDA as a first-class backend**: open, and the owner's machine has an RTX 5070 Ti.
   The optimizer already takes any `Accelerator`, but only OpenCL (`gfx1036`) was
   ever run; the tests hard-code `builder.OpenCL()`. Recommendation: test on CUDA
   before v1, and let the builder choose the device like APT does. `Auto` falls back
   to the CPU and reports why; an explicit CUDA request never falls back silently. A
   risk to measure first: APT found that ILGPU 1.5.3 drops libdevice wrappers on
   compute_100+ (Blackwell, which the 5070 Ti is) and fixes it with a post-link (APT
   `src/Execution/BOOT.md`). This package pins ILGPU 1.5.1, so the ILGPU version is
   part of this item.
10. **The GPU tests in CI on ILGPU's CPU accelerator**: recommended. APT and CPM run the
    same kernels on the CPU accelerator in hosted CI and on a GPU only locally or on a
    self-hosted runner. That would close the root's "GPU tests run in no CI" item. The
    CPU accelerator needs a device sized from `ProcessorCount` (its default is 16
    threads). It is an oracle, not a production path: CPM measured it about 10× slower
    than plain .NET threads (CPM `HISTORY.md`).
11. **A counter-based device RNG**: recommended. Today each individual keeps its own
    `XorShift32` state in device memory: 32 bits of state, so at most 2^32 distinct
    doubles per stream. CPM measured that streams made by jumping one generator are
    shifts of one sequence, not independent samples (CPM `src/Random/BOOT.md`,
    2026-09-23). A counter-based generator gives a value as a pure function of (seed,
    individual, generation, draw): reproducible, independent per individual, and with
    no state buffer. That also gives item 3 its seed.
12. **Kernel guard tests from APT**: recommended, in this node's protocol tests or its
    own.
    - An IL walk from the kernels that refuses `throw`, `newarr`, `newobj` and `box`,
      so a kernel that cannot compile fails hosted CI, not the first GPU run.
    - An allow-list of `Math` members.
    - No constant on the left of an ordered floating comparison. ILGPU flips its NaN
      ordering when it swaps the operands, so CUDA and CPU disagree (APT
      `BOOT.md`, the ILGPU 1.5.3 constraint).
    - `NaN` handled by explicit `IsNaN` tests, not by `<`. Today a `NaN` parent is
      never replaced (selection uses `trial < parent`), while the CPU package ranks
      `NaN` worst.
    - Host transfers only through the pinning overloads. APT lost downloads through
      `CopyToCPU(ref T, long)`. This package uses `GetAsArray1D`/`GetAsArray2D`,
      which pin, so it is safe today; the guard keeps it that way.
