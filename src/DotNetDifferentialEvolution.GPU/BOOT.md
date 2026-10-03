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

→ [ACCEPTANCE.md](ACCEPTANCE.md)

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

## v1 design ⏳

Decided by the owner on 2026-10-03: every recommendation of the redesign proposals was
accepted (full text, sources, the CUDA table → HISTORY.md#redesign-proposals-2026-10-03).
Design mode: none of this exists yet. The public contract is `API.md`, `## v1 contract
⏳`; the checks, frozen before any code, are in [ACCEPTANCE.md](ACCEPTANCE.md). Coding
starts only after the owner approves this design.

### Decisions

- **Version 1.0.0**, a breaking release: every public type of 0.x is removed or
  replaced. Released from tags `gpu-v*` by its own job in `release.yml`; the CPU job
  keeps `v*`.
- **ILGPU 1.5.3.** The figure that decided it (2026-10-03): ILGPU 1.5.1 fails PTX JIT on
  the RTX 5070 Ti through CUDA; 1.5.3 passes both GPU tests there and on OpenCL.
- **`double` only; DE/rand/1/bin only.** jDE comes in the next minor version.
- **The objective returns its value.** A struct implementing `IGpuFitnessFunction`,
  `double Evaluate(GeneView genes)`. It gets a read-only view of one individual, and the
  kernel writes the value.
- **The result is an `ISolution`** from `DotNetOptimization.Abstractions` 1.0.0, a new
  package dependency (outside the tree, as for the CPU package).
- **A staged builder**, like the CPU one, carrying the objective's type through the
  stages.
- **The device.**
  - `Auto` tries CUDA, then OpenCL, then ILGPU's CPU accelerator, and records what it
    chose and why it skipped the others.
  - An explicit device that is missing makes `Build` throw. It never falls back.
  - Alternatively, the caller passes in its own ILGPU `Accelerator`, which the optimizer
    never disposes.
- **The RNG is Philox4x32-10, counter-based.** Its known-answer vectors are copied from
  Random123's `kat_vectors` when it is implemented, with the source cited.
- **F and CR are validated** (F finite and > 0, CR in [0, 1]). This deliberately differs
  from the CPU package, which accepts any value.

### Invariants of v1

Each one is checked by the item of the same number in [ACCEPTANCE.md](ACCEPTANCE.md).

1. **The semantics are those of `docs/ALGORITHMS.md`, §§2–3 and §9.**
   - The initial population is uniform in the box, and the evaluation count starts at N.
   - r1, r2 and r3 are mutually distinct and differ from i.
   - Crossover is binomial and includes `jrand`.
   - Out-of-box genes are repaired to the midpoint toward the parent.
   - Survival is `f(u) <= f(x)`; `NaN` is worse than every real value, and two `NaN`s
     are not a tie.
   - The best individual: `NaN` is worst, and a tie goes to the lowest index.
2. **The kernel, not the user, prevents races.** Thread i writes only trial slot i and
   next slot i, and the objective gets a view it cannot write through.
3. **A random draw is a pure function of (seed, individual, generation, draw index).**
   It is the same on every backend: the RNG uses integer arithmetic only.
4. **Reproducibility.**
   - The same seed, device and package and ILGPU versions give a bit-identical result.
   - Across backends the draws are identical, but results may differ: FMA contraction
     and math-library ULPs are the backend's. This is stated, not hidden.
5. **No host round trip per generation.** The limits are host counters. The population
   reaches the host only when the observer is due and once at the end.
6. **`RunAsync` is asynchronous, as in the CPU package.**
   - It returns before the run ends.
   - A token is observed between generations and ends the task as canceled.
   - A second call after the run returns the same task; a call during the run throws.
7. **Resource ownership.**
   - `Dispose` frees what the optimizer allocated, and only that.
   - There is no `GC.Collect`.
   - A caller-owned `Accelerator` is never disposed.
8. **Kernel code compiles on every backend.**
   - Nothing reachable from a kernel contains `throw`, `newarr`, `newobj` or `box`.
   - `Math` calls are limited to an allow-list.
   - No constant sits on the left of an ordered floating-point comparison.
   - `NaN` is tested with `IsNaN`.
   - Host transfers use only the pinning overloads.

### Decomposition of v1

| Node | Public | Role |
|---|---|---|
| this node | yes | builder, optimizer, result, `GpuDevice`, snapshot for the observer |
| `Objectives/` | yes | `IGpuFitnessFunction`, `GeneView` |
| `Devices/` | no | device selection, the CUDA math-probe kernel APT uses |
| `Kernels/` | no | init and generation kernels, one thread per individual; the DE step as static functions over a draw source, so tests can script the draws; populations individual-major (`N·D` genes, `N` fitness), two buffers swapped per generation plus a trial buffer |
| `Random/` | no | Philox4x32-10; uniform `[0, 1)` from 53 bits; Lemire index draws |

The dependencies run one way: the root depends on `Objectives`, `Devices` and `Kernels`;
`Kernels` depends on `Objectives` and `Random`. `Devices` and `Random` depend on nothing.

Every 0.x node goes in v1, each together with its code in the same commit:
- `Interfaces`, `Models`, `Controllers/Kernels`;
- `MutationStrategies`, `SelectionStrategies`, `RandomGenerators`,
  `PopulationSamplingMakers`, `TerminationStrategies`;
- each of their `Interfaces` children.

That also settles the owner's open question on the `*/Interfaces` nodes (see
`## Decomposition`).

Tests: `GPU.Test` gets the children `Random/`, `Kernels/`, `Builder/`, `Devices/` and
`EndToEnd/`. Every test runs on the CPU accelerator in hosted CI. The same cases run on
a real device locally under `Category=Gpu`. The kernel guards (invariant 8) become facts
in `Protocol.Tests`, adapted from APT with the source cited.

### Order of work

Each slice is one PR, merged at the owner's word:

1. `Random`
2. `Objectives` and `Kernels`
3. `Devices`
4. the root, with the 0.x nodes deleted and `README.md` rewritten
5. CI and `release.yml`, plus the guard facts
6. local runs on CUDA and OpenCL; then 1.0.0, at the owner's word

The child node pairs are written at the start of the slice that codes them, from this
section. Each must pass the sufficiency check of `prompts/03` before its code.
