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

**Symmetric with the CPU package** (decided and built 2026-10-05, in the v1 PR →
HISTORY.md#symmetry-decided-2026-10-05): the five fixed-F schemes, jDE, JADE, SHADE and
L-SHADE, and the stagnation rule beside the two limits, by the CPU package's names,
parameters and defaults, with its semantics draw for draw. Between generations the
device keeps what the configuration needs: the best index, the fitness ranking, the
archive, the adaptation state, L-SHADE's population size ([Bookkeeping](Bookkeeping/API.md)).

Not goals of 1.0.0: `float`; a caller's own scheme, variant, parameter provider,
selection, stop rule, local search or initial sampling (the CPU package's open
interfaces: kernel code is a struct compiled into the kernel); a host-side objective.

⚠ 2026-10-05: was "Not goals of 1.0.0: `float`, jDE and the other adaptive variants, other
mutation schemes, stop rules other than the two limits, a host-side objective"; now the
built-in schemes, variants and stop rules are goals → HISTORY.md#symmetry-decided-2026-10-05

## Invariants

Each is checked by the item of the same number in [ACCEPTANCE.md](ACCEPTANCE.md).

1. **The semantics are those of `docs/ALGORITHMS.md`, §§2–3 and §9.**
   - The initial population is uniform in the box, and the evaluation count starts at N.
   - r1, r2 and r3 are mutually distinct and differ from i.
   - Crossover is binomial and includes `jrand`.
   - Out-of-box genes are repaired to the midpoint toward the parent.
   - Survival is `f(u) <= f(x)`; `NaN` is worse than every real value, and two `NaN`s
     are not a tie.
   - The best individual: `NaN` is worst, and a tie goes to the lowest index. (The CPU
     package does the same with one worker or a generation strategy; with several workers
     and a fixed scheme its tie can go to a higher index, `docs/ALGORITHMS.md` §9.10.)
   - The other schemes, jDE, JADE, SHADE and L-SHADE (§§3–7 and §9), selection with
     and without ties, the archive and the stagnation rule are the CPU package's:
     the same draws give the same trial, F and CR bit for bit (ACCEPTANCE.md, S2–S6;
     Bookkeeping/ACCEPTANCE.md, S7–S12).
2. **The kernel, not the user, prevents races.** Thread i writes only trial slot i and
   next slot i, and the objective gets a view it cannot write through.
3. **A random draw is a pure function of (seed, individual, generation, stream, draw
   index).** Stream 0 is the generation's; stream 1 the archive's slot draws.
   It is the same on every backend: the RNG uses integer arithmetic only.
4. **Reproducibility.**
   - The same seed, device and package and ILGPU versions give a bit-identical result.
   - Across backends the draws are identical, but results may differ: floating-point
     code generation and math-library ULPs are the backend's. Measured once, 2026-10-03:
     the CPU accelerator and OpenCL agreed bit for bit on a Sphere run, CUDA did not.
5. **No host round trip per generation.** The limits are host counters. The population
   reaches the host only when the observer is due and once at the end. With a
   stagnation limit, the stop rule runs on the device, and its control block of a few
   words reaches the host every 16 generations and before each observer call.
   ⚠ 2026-10-05: the control block added → HISTORY.md#symmetry-decided-2026-10-05
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

Outside the tree: ILGPU, exactly 1.5.3; `DotNetOptimization.Abstractions` 1.0.0
(`ISolution`); .NET 8; for CUDA, a CUDA Toolkit's libnvvm and `libdevice.10.bc`
(`Devices/LibDevice/BOOT.md`; tested with 12.9 and 13.4). ILGPU 1.5.3 is the version that
passes on the RTX 5070 Ti (compute 12.0): 1.5.1 failed PTX JIT there (measured 2026-10-03,
HISTORY.md#redesign-proposals-2026-10-03); the post-link asserts it.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- **Kernel code** (the objective, the DE step, the RNG): value types, no virtual calls, no
  managed allocation, no exceptions, no strings. A violation surfaces when ILGPU compiles
  the kernel, in `Build`, not when C# compiles.
- **ILGPU must see every type a kernel touches.** It emits its launchers into a dynamic
  assembly named `ILGPURuntime`: the package grants it `InternalsVisibleTo`, and a
  caller's objective type must be public or do the same (`Objectives/API.md`).
- **CUDA math is libdevice's**, as APThermo has it: a CUDA context is built with
  `LibDevice`, and every kernel loads through `Devices.KernelLoader`, which completes the
  libdevice wrappers ILGPU 1.5.3 leaves undefined for compute 10.0 and newer
  (`Devices/LibDevice/BOOT.md`). No ILGPU.Algorithms. The package's own kernels call no
  `Exp` or `Pow`; JADE's, SHADE's and L-SHADE's call `Log`, `Cos` and `Tan` (the CPU
  package's Gaussian and Cauchy samplers), the fixed schemes' and jDE's none of them (the
  parameter rule is a type argument of the generation kernel). A caller's objective can
  call any.

  Measured once, 2026-10-03, RTX 5070 Ti, CUDA Toolkit 13.4, a scratch program outside the
  tree: an objective of `Exp(−x) + Log(x)² + Pow(x, 1.37)` per gene, N = 65 536, D = 10,
  300 generations, seed 1, the run alone (`Build` excluded), median of three runs after a
  warm-up: 2 864 ms with ILGPU.Algorithms (commit `860ba28`), 159.5 ms with libdevice
  (about 18 times faster). The two results differed in the last bit.

  ⚠ 2026-10-03: was "every context is built with `EnableAlgorithms()`", which missed the
  4-ULP bound of check D2 on CUDA (Exp 195, Log 9 430, Pow 24 ULP), now libdevice →
  HISTORY.md#libdevice-port-2026-10-03
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
| [Devices](Devices/API.md) | no | device selection with fallback reasons, accelerator ownership, kernel loading, the math probe kernel; child [LibDevice](Devices/LibDevice/API.md): libdevice and the post-link |
| [Kernels](Kernels/API.md) | no | the init and generation kernels, the DE step over a draw source; every scheme, the parameter rules, selection with or without ties |
| [Bookkeeping](Bookkeeping/API.md) | no | between generations, on the device: best index, ranking, archive, adaptation, L-SHADE's reduction, the stagnation rule |
| [Random](Random/API.md) | no | Philox4x32-10, uniform doubles from 53 bits, Lemire index draws |

The dependencies run one way: this node uses all of them; `Kernels` uses `Objectives` and
`Random`; `Bookkeeping` uses `Kernels`, `Random` and `Devices`; `Devices`, `Objectives`
and `Random` use nothing. No cycle (by the `using` directives, 2026-10-05).

Tests: `tests/DotNetDifferentialEvolution.GPU.Test`, run on the CPU accelerator in hosted
CI and on CUDA and OpenCL locally under `Category=Gpu`. The kernel guards (invariant 8)
are facts in `tests/DotNetDifferentialEvolution.Protocol.Tests`.
