# BOOT.md — DotNetDifferentialEvolution.GPU

## Purpose

Differential Evolution with the whole population on a GPU, through ILGPU. Each generation
is one kernel launch in which every thread builds, evaluates and selects one individual;
the objective is a struct compiled into that kernel. The package exists for objectives
cheap enough per call and populations large enough that one GPU thread per individual
beats CPU threads.

Version 1.0.0, built 2026-10-03 on the design the owner approved that day: DE/rand/1/bin
in `double`, a staged builder, an `ISolution` result, a counter-based RNG, CUDA, OpenCL or
ILGPU's CPU accelerator. It replaces every public type of 0.x (0.1.0, 0.0.2 and 0.2.0 on
nuget.org, 2024-08). The 0.x description, the design text and what the implementation
decided against it → HISTORY.md#v1-built-2026-10-03.

Not goals of 1.0.0: `float`, jDE and the other adaptive variants, other mutation schemes,
stop rules other than the two limits, a host-side objective.

## Invariants

Each is checked by the item of the same number in [ACCEPTANCE.md](ACCEPTANCE.md).

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
   - Across backends the draws are identical, but results may differ: floating-point
     code generation and math-library ULPs are the backend's. Measured once, 2026-10-03:
     the CPU accelerator and OpenCL agreed bit for bit on a Sphere run, CUDA did not.
5. **No host round trip per generation.** The limits are host counters. The population
   reaches the host only when the observer is due and once at the end.
6. **`RunAsync` is asynchronous, as in the CPU package.**
   - It returns before the run ends; the run has a thread of its own.
   - A token is observed between generations and ends the task as canceled.
   - A second call after the run returns the same task; a call during the run throws.
7. **Resource ownership.**
   - `Dispose` frees what the optimizer allocated, and only that.
   - There is no `GC.Collect`.
   - A caller-owned `Accelerator` is never disposed.
8. **Kernel code compiles on every backend.**
   - Nothing reachable from a kernel contains `throw`, `newarr`, `newobj` of a reference
     type or `box`.
   - `Math` calls are limited to an allow-list.
   - No constant sits on the left of an ordered floating-point comparison.
   - `NaN` is tested with `IsNaN`.
   - Host transfers use only the pinning overloads.

**The package depends on nothing in this repository.** No reference to the CPU package;
both packages depend on `DotNetOptimization.Abstractions`. Held by the csproj.

## Dependencies

None.

Outside the tree: ILGPU 1.5.3 and ILGPU.Algorithms 1.5.3; `DotNetOptimization.Abstractions`
1.0.0 (`ISolution`); .NET 8. ILGPU 1.5.3 is the version that passes on the RTX 5070 Ti
(compute 12.0): 1.5.1 failed PTX JIT there (measured 2026-10-03,
HISTORY.md#redesign-proposals-2026-10-03).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Kernel code** (the objective, the DE step, the RNG): value types, no virtual calls, no
  managed allocation, no exceptions, no strings. A violation surfaces when ILGPU compiles
  the kernel, in `Build`, not when C# compiles.
- **ILGPU must see every type a kernel touches.** It emits its launchers into a dynamic
  assembly named `ILGPURuntime`: the package grants it `InternalsVisibleTo`, and a
  caller's objective type must be public or do the same (`Objectives/API.md`).
- **Every context is built with `EnableAlgorithms()`**, so `Exp`, `Log` and `Pow` have an
  implementation on PTX. Whether that implementation meets APT's 4-ULP bound on CUDA is
  check D2.
- The package ships `README.md` and `ILGPU_LICENSE` from this directory and the
  repository's `LICENSE`. It is packed from a `gpu-v*` tag by `release.yml`; CI packs it
  without publishing.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- **No reference to ILGPU from the CPU package, and none from here to the CPU
  package.** The root's taboo; the two packages stay siblings.
- **No host transfer in the generation loop** other than the observer's, and no transfer
  outside `PopulationTransfers` (check 5a).
- **No `GC.Collect`** (check 7a). 0.x forced one on `Dispose`.
- **No `GeneratePackageOnBuild`.** Packages are packed from a tagged commit.
- **No package validation against 0.x.** 1.0.0 replaces every public type on purpose;
  validation starts with 1.0.0 as its baseline.

## Decomposition

| Node | Public | Role |
|---|---|---|
| this node | yes | builder, optimizer, result, `GpuDevice`, observer and snapshot; `KernelLauncher`, `PopulationTransfers`, `BestPick`, `RunSettings` inside |
| [Objectives](Objectives/API.md) | yes | `IGpuFitnessFunction`, `GeneView` |
| [Devices](Devices/API.md) | no | device selection with fallback reasons, accelerator ownership, the math probe kernel |
| [Kernels](Kernels/API.md) | no | the init and generation kernels, the DE step over a draw source |
| [Random](Random/API.md) | no | Philox4x32-10, uniform doubles from 53 bits, Lemire index draws |

The dependencies run one way: this node uses all four; `Kernels` uses `Objectives` and
`Random`; `Devices`, `Objectives` and `Random` use nothing. No cycle (by the `using`
directives, 2026-10-03).

Tests: `tests/DotNetDifferentialEvolution.GPU.Test`, run on the CPU accelerator in hosted
CI and on CUDA and OpenCL locally under `Category=Gpu`. The kernel guards (invariant 8)
are facts in `tests/DotNetDifferentialEvolution.Protocol.Tests`.
