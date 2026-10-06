# HISTORY.md — DotNetDifferentialEvolution.GPU

Append-only, newest first (AGENTS.md §15). Read by following a pointer, not at start.

<a id="stop-word-exits-2026-10-06"></a>
## 2026-10-06 — the stop word is read at every exit of a run

The algorithm audit of 2026-10-06 found two exits of `Run` that skip the stop word:
- **A limit beside the stagnation rule.** The loop ended through `LimitReached` with no last
  read, so a rule that fired between two reads left the host's counters, and the buffer the
  result is taken from, at the limit instead of the stopping generation. The public builder
  takes one stop condition, as the CPU builder does, so no caller reaches it; `RunSettings`
  takes both.
- **A cancellation after the rule fired.** The token was checked before the read, so a
  cancellation in the window between the stopping generation and the next read (at most 15
  generations, each one launch that does nothing) gave a canceled task. The CPU package tests
  its termination before its cancellation at the same barrier and completes.

The owner: "давай отдельный PR для GPU". Both exits now read the stop word first (check S18).
The per-generation hook the test cancels from is internal; nothing public changes.

<a id="jde-strict-selection-2026-10-06"></a>
## 2026-10-06 — jDE refuses ties, as its paper does, in both packages

An algorithm audit of 2026-10-06 (two read-only auditors, the CPU package against the papers,
this package against the CPU package) read Brest et al. 2006 (IEEE TEVC 10(6), p. 646 ff.,
§III-C): "x_{i,G+1} = u_{i,G+1} if f(u_{i,G+1}) < f(x_{i,G})", and "If, and only if, the trial
vector yields a better cost function value than x_{i,G}, then x_{i,G+1} is set to
u_{i,G+1}". Both packages let a jDE tie survive: the CPU package's `JdeVariant` installed the
engine's default selection, whose tie rule the CPU tree had marked as without a source
(`Algorithms/Jde/BOOT.md`, `Variants/BOOT.md`). The owner: "Давай (а)", jDE to its paper's
rule. With strict selection, survival and improvement are one event, so jDE's inheritance
(on survival) now follows improvement; a tie keeps the parent and its F and CR.

Changed openly: S5 (jDE moves from the ties-accepted list to the refused one) and S6 (the
inheritance "exactly where the trial improved", a tie keeping the parent; red: inheritance on
a tie). `TieRuleTests` needed no change: it takes each configuration's rule from the CPU
variant.

<a id="libdevice-kept-2026-10-05"></a>
## 2026-10-05 — libdevice kept, though APThermo has left it

APThermo, whose libdevice post-link this package adopted from its commit `5fdd82c`, left
libdevice on 2026-10-05 (its merge `a8808127`; its `HISTORY.md`, `ilgpu-libdevice-retired`
and `allow-list-own-math`): its own correctly rounded `exp`, `log` and `pow`
(`src/Thermo/Elementary`), a post-link that marks every double `mul`, `add` and `sub`
`.rn`, and CUDA equal to the CPU accelerator bit for bit, at a cost it measured of +74 % on
its CUDA kernel. Doing the same here would need correctly rounded `Cos` and `Tan` as well,
which APThermo does not have.

The owner, told this: "если libdevice не ломает логику, то давай его оставим". It does not:
the step, the samplers and the bookkeeping are held to the CPU package bit for bit on the
host and the CPU accelerator (S2–S12); on CUDA, libdevice's `Exp`, `Log`, `Pow`, `Sqrt`,
`Cos` and `Tan` are within 2 ULP of `System.Math` (D2, S15), every configuration converges
(S13) and repeats bit for bit (S14). What it costs is invariant 4 as written since v1: across
backends the draws are equal and the results may differ. Nothing changes.

<a id="symmetry-built-2026-10-05"></a>
## 2026-10-05 — symmetry built; what the red runs found in the tests

Built on the design below (commits `2cb39ab`, the code; `d1ecb52`, the tests). Each of
S1–S17 and B2 was then run red once on its named mutation, from a commit, the source
restored by `git checkout` after each run. Five runs first showed the tests, not the
code, short; each test was strengthened and the run repeated (the frozen checks'
text unchanged):

- **S5**, JADE accepting ties, stayed green: the cases called `Selection.Outcome` with
  the tie rule as an argument, so no case tied a configuration to its rule. Added
  `TieRuleTests`: each configuration, run on a flat objective, replaces tied parents
  exactly when the CPU variant's `SelectionStrategy` accepts a tie.
- **S6**, inheritance on improvement only, stayed green: jDE keeps the parent's F and
  CR nine times in ten, and the five ties of seed 91 all kept them. The objective became
  ⌊Σx²/4⌋, and a tie with a new F or CR is asserted.
- **S7**, the memory index advanced without success, stayed green: the device case
  had a success in every generation. It now runs three generations, the first without.
- **S9**'s expected order was computed with `FitnessOrder.KeyOf`, the code under test;
  it now maps `NaN` itself.
- **S12**, the streak reset on equality, hung: the stop rule never fired and the run
  had no other bound. An observer now ends any run of the test at generation 10 000.
- **B2**'s archive case asked for 2·10¹⁰ genes, which an off-by-far guard also refuses;
  it now asks for 2³¹, one more than `int.MaxValue`.

Three mutations did not compile under the analyzers (IDE0059, IDE0060, CA1508) and were
rewritten to the same defect. A fourth, for S13 (the trial's fitness replaced by a
constant `NaN` in `Selection.Outcome`), left best/1 and current-to-best/1 converging on
CUDA only: a constant on the left of an ordered comparison after inlining, the hazard
check 8c keeps out of the package, is the likely reason (not investigated further). The
mutation used is "the survivor is always the parent", red in all 18 cases.

S7–S12 moved unchanged to `Bookkeeping/ACCEPTANCE.md` when the package's file reached its
limit (AGENTS.md §15).

<a id="symmetry-decided-2026-10-05"></a>
## 2026-10-05 — symmetry with the CPU package decided, in the v1 PR

The owner asked whether the package is symmetric with the CPU package ("стратегии такие
же имеются?"). It was not: 1.0.0 had DE/rand/1/bin and two limits. The owner: "Давай в
этом PR", and asked for what has been published on these algorithms on a GPU.

**Scope.** Every built-in of the CPU package that a caller reaches by name:
- the five fixed-F schemes `WithDefaultMutationStrategy` (rand/1),
  `WithBestMutationStrategy`, `WithCurrentToBestMutationStrategy`,
  `WithRandTwoMutationStrategy`, `WithBestTwoMutationStrategy`;
- the four variants `WithJde`, `WithJade`, `WithShade`, `WithLShade`, with the CPU
  package's parameter names, order and defaults;
- the stagnation stop rule beside the two limits.

Out of scope, because kernel code is a struct compiled into the kernel and these are
open interfaces of the CPU package: a caller's `IMutationStrategy`, `IDeVariant`,
`IControlParameterProvider` (and so the dithered provider), `ISelectionStrategy`,
`ITerminationStrategy`, `ILocalSearchRefiner`, `IPopulationSamplingMaker`.

**The semantics are the CPU package's, draw for draw.** Each scheme and each parameter
rule consumes draws in the CPU step's order and computes with its formulas: the Gaussian
is the CPU package's two-uniform transform (`RandomDistributionHelper.NextGaussian` for a
provider other than `SeededRandomProvider`, the sine half discarded), the Cauchy its
`location + scale·tan(π(u − 0.5))`. So the same draws give the same trial, F and CR bit
for bit on the host, as check 1g already holds for rand/1. Where the CPU package is
sequential and the GPU is not, the result is the same set of operations in a fixed order:
- **Best index and ranking** are computed on the device every generation the
  configuration needs them, `NaN` worst, ties to the lower index. The CPU package's
  `Array.Sort` leaves ties among equal keys unordered; this order is one of its orders.
- **Adaptation sums** (JADE's μ, SHADE's memory) are summed in index order within chunks
  of 1 024 and the chunk sums in chunk order. For N ≤ 1 024 that is the CPU package's
  order, bit for bit; above it, rounding differs.
- **The archive** follows `AdaptiveStrategyBase.UpdateArchive`: improved parents in index
  order fill the free slots, the rest overwrite a uniform slot each, and of two parents
  drawn to one slot the later index stays. Each overflowing parent draws its slot from its
  own stream; the CPU package draws them from one sequential provider.
- **L-SHADE's population sizes** are computed on the host, from the evaluation count,
  which the host knows exactly; the survivors are the first N of the ranking, stored in
  ranking order, as in `LShadeStrategy`.

**Alternatives considered.**
1. *A different, GPU-friendly design per variant* (no archive, Normal F, non-distinct
   donors, as EvoX does). Rejected: not the same algorithms, and not symmetric.
2. *ILGPU.Algorithms' radix sort and reductions.* Rejected: removed on 2026-10-03
   (check L8), and their order of summation is not ours to fix.
3. *One thread for all bookkeeping.* Rejected: O(N) on one GPU thread per generation.
4. *Shared-memory reductions and sorts* (explicitly grouped kernels). Deferred: chunked
   passes over global memory need no new kernel loading path; they are measured once
   built, and replaced if they dominate.
5. *Ranking by counting* (rank i = the number of keys before it) for N ≤ 8 192, one
   launch; a bitonic network over global memory above it. Taken: both give the one total
   order by (key, index), so the choice is invisible in the result.
6. *Stagnation read every generation* (one value, one synchronisation). Rejected for a
   device control block: the stop rule runs on the device after each generation, every
   later kernel does nothing once it has fired, and the host reads the block every 16
   generations and when the observer is due. The result is that of a check every
   generation.

**Changed openly in the frozen v1 checks** (each with ⚠ in `ACCEPTANCE.md`):
- 8b: `Cos`, `Tan` and `IsFinite` join the kernel `Math` allow-list, for the CPU
  package's samplers and SHADE's weight test; D3 holds `Cos` and `Tan` on CUDA to the
  4 ULP of D2.
- Invariant 5 and check 5b: with a stagnation limit, a control block of a few words is
  read every 16 generations.
- B1's row "`populationSize < 4`": the minimum is now the scheme's, refused by `Build`
  with `InvalidOperationException`, as the CPU package does; `WithPopulationSize` refuses
  N < 1 only.

**What has been published** (searched 2026-10-05 by a research agent; sources read in
full unless marked):
- Janssen, Pullan, Liew, "GPU Based Differential Evolution: New Insights and Comparative
  Study", arXiv:2405.16551 (2024): the one recent survey. Only one GPU DE code is fully
  open (cujDE); SHADE-family DE on a GPU is named as future work. All-on-device designs
  measured 6–10.6 times faster than ones that copy fitness to the host every generation.
- cujDE (Boiani, Dominico, Parpinelli, ISDA 2018; github.com/mateuz/cujDE): jDE in CUDA,
  `float`, curand XORWOW seeded from `random_device` and indexed `rng[id_d * id_p]`, so
  threads share states: neither reproducible nor race-free. Donors by cyclic
  displacement, not uniform.
- EvoX (Huang et al., IEEE TEVC 2025): `JaDE` and `SHADE` on PyTorch, with F from a
  Normal instead of a Cauchy, no archive, donors neither distinct nor different from i,
  and a crossover mask from Gaussian draws. No jDE, no L-SHADE. evosax: classic DE only.
- Mexicano et al., DSPAI 1(4), 2022: a CUDA "JADE" run with fixed F and CR on Sphere
  only. 62.6 times faster at D = 100, N = 10 000; 0.33 times (slower) at D = 30, N = 100.
- No GPU implementation of L-SHADE's population reduction or of a JADE/SHADE archive was
  found. Tanabe's `lshade.cc` (CEC 2014) is the reference the CPU package already cites.
- What was taken from them: the archive by an exclusive scan and an integer `atomicMax`
  for the later index (the agent's construction, matching the serial loop); the host-side
  L-SHADE schedule; ranking by counting for small N; stagnation as a device flag read in
  batches. Every listed deviation of EvoX and cujDE is one this design avoids by parity
  with the CPU package.

Checks frozen for it before its code: `ACCEPTANCE.md`, S1–S17.

<a id="libdevice-port-2026-10-03"></a>
## 2026-10-03 — CUDA math through libdevice, as APThermo does; ILGPU.Algorithms removed

D2 was red on CUDA with `EnableAlgorithms()`: Exp 195, Log 9 430, Pow 24 ULP at worst on
the RTX 5070 Ti (ACCEPTANCE.md, D2). The owner, the same day, after asking how
APThermo (`C:\Projects\AerospacePropellantThermodynamics`, commit `5fdd82c`) does it:
"Давай сделаем также как в APThermo".

What that means here, adapted from APT's `src/Execution/LibDevice` (`LibDeviceLocator`,
`LibDevicePostLink`, `CudaWslDevices`) and `src/Execution/AcceleratorChoice.cs`:

1. **No ILGPU.Algorithms.** A CUDA context is built with `Math(MathMode.Default)` and
   `LibDevice(dll, bitcode)`, so ILGPU emits calls to its libdevice wrappers; OpenCL and
   the CPU accelerator use their own math, as before.
2. **A post-link completes the wrappers.** ILGPU 1.5.3 calls the `__nv_*` wrappers and,
   for compute 10.0 and newer, defines none of them (measured here on `sm_120`: four
   calls, no definition). Every CUDA kernel is compiled, completed from ILGPU's own
   fragments through libnvvm, trial-loaded and only then loaded.
3. **CUDA needs a CUDA Toolkit** for libnvvm and `libdevice.10.bc`, found as APT finds
   them. Without one, an explicit CUDA request fails naming both files and Auto goes on to
   OpenCL with that reason. A caller-owned CUDA accelerator brings its own context, and
   its kernels are completed with its own libnvvm when that context has `LibDevice`.
4. **CUDA counts as opened only after the probe kernel loads** through the post-link, and
   libnvvm is checked before the accelerator exists, so a bad library never reaches the
   device.
5. **The WSL workaround** for a second CUDA context of a process, as APT's.

Not taken from APT: its explicit libnvvm and libdevice paths in the engine options (the
package has no options object; a caller-owned accelerator covers the case); its all-cores
CPU device, its launch budget and kernel cache (the package has its own launcher).

Checks frozen for it before its code: ACCEPTANCE.md, L1–L9.

<a id="v1-built-2026-10-03"></a>
## 2026-10-03 — v1 built; the 0.x description and the v1 design moved in full

The 0.x code was deleted and v1 written on the branch `feat/gpu-v1`. `BOOT.md`, `API.md`
and `ACCEPTANCE.md` now describe v1 as built. Moved here, verbatim from the commit before:
the 0.x parts of the three documents and `BOOT.md`'s `## v1 design ⏳`.

What the implementation decided or changed against the approved design, each said where
it now lives:

1. **Order of work.** The design had one PR per slice, 0.x deleted in slice 4. The owner
   asked on 2026-10-03 for the maximum diagnostics first and for one PR at the end. The
   0.x code went first, since bringing it to zero warnings only to delete it would have
   been wasted work; all six slices are on one branch.
2. **The unseeded seed** is one draw of `RandomNumberGenerator.GetInt32(int.MaxValue)`,
   not of `Random.Shared` as `API.md` said: the analyzers refuse `System.Random` (CA5394).
   Nothing observable changes; unseeded runs were never reproducible.
3. **Objective visibility.** ILGPU emits its launchers into a dynamic assembly named
   `ILGPURuntime`; the first smoke run failed with "Access is denied" on a private nested
   objective. The package grants `InternalsVisibleTo("ILGPURuntime")` for its own kernel
   types, and `Objectives/API.md` tells callers what their objective type needs.
4. **More argument errors** than the design's table: non-finite bounds, `N·D` above
   `int.MaxValue`, an undefined `GpuDevice`, an accelerator other than CUDA, OpenCL or CPU.
   In `API.md`'s error table.
5. **The evaluation limit runs at least one generation**: the limits are tested after each
   generation, as the CPU package tests termination at a generation boundary.
6. **Kernel entry points are found, not registered**: any method whose first parameter is
   an `Index1D`. In `Kernels/BOOT.md`.
7. **The Philox layout**: key `(seed, 0)`, counter `(block, individual, generation, 0)`,
   generation 0 the initial sampling. In `Random/API.md`.
8. **The CPU accelerator is ILGPU's default device**, not one sized from `ProcessorCount`
   as proposal 10 suggested. In `Devices/BOOT.md`.
9. **Auto falls back on a missing device or a failed accelerator**, not on a kernel that
   later fails to compile on the device it opened. In `Devices/BOOT.md`.

First measurement, one run each, compile included (2026-10-03): a Sphere 5-D smoke run
(N 50, F 0.5, CR 0.9, 300 generations, seed 1) reached 1.6785924287527606e-27 on the CPU
accelerator and on OpenCL (`gfx1036`), bit for bit the same, and 1.7402331143391134e-27
on CUDA (RTX 5070 Ti). Different on CUDA, as `BOOT.md`'s invariant 4 allows; the cause
(FMA contraction or another code-generation difference) was not established.

Original text of `BOOT.md`, from `## Purpose` to the end of `## Decomposition`:

### Purpose

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

### Invariants

- **Every strategy and the objective are generic struct parameters of the
  controller.** ILGPU specialises the kernels per combination, with no virtual calls on
  the device. Held by the generic constraints in
  Controllers/Kernels (`Controllers/Kernels/API.md`).
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

### Dependencies

None.

Outside the tree: ILGPU 1.5.1 and ILGPU.Algorithms 1.5.1; .NET 8. The tests need a
device ILGPU can open through OpenCL.

### Constraints

Inherited from the root (BOOT.md (`../../BOOT.md`)). In addition:

- Kernel code (the objective, mutation, selection, random generator, device population)
  must compile under ILGPU: value types, no virtual calls, no managed allocation beyond
  small fixed local arrays, no exceptions. Failures appear at run time, when the
  optimizer's constructor loads the kernels.
- Two analyzer rules are off for this package and its tests in `.editorconfig`, with
  reasons: CA1815 (kernel structs are never compared) and CA1814 (ILGPU's 2D
  allocation takes `double[,]`). Everything else of the repository policy applies.
- The package ships `README.md` and `ILGPU_LICENSE` from this directory and the
  repository's `LICENSE`.

### Acceptance criteria

→ ACCEPTANCE.md (`ACCEPTANCE.md`)

### Taboos

- **No reference to ILGPU from the CPU package, and none from here to the CPU
  package.** The root's taboo; the two packages stay siblings.
- **No host round trip per generation in the core loop.** The random-number design
  was rebuilt on 2024-08-10 (`1ad5a86`) to remove one.
- **No `GeneratePackageOnBuild`.** Removed at the import (`54cf002`); packages are
  packed from a tagged commit (root `BOOT.md`).

### Decomposition

One directory per role (controller and kernels, mutation, selection, random generator,
initial sampling, termination, models), plus the package-level `Interfaces`. Every
strategy directory has an `Interfaces` subdirectory holding its one contract (the
controller's is `Controllers/Kernels/Interfaces`; `Models` has none); the list is in
API.md (`API.md`), `## Children`. No cycles between
them (textual estimate, 2026-10-02).

The `*/Interfaces` directories are nodes of their own, each with a single interface.
Owner's decision, 2026-10-02: describe them as they are; merging each into its parent
would break the public namespaces, and is left for the GPU redesign.

Original text of `BOOT.md`, `## v1 design ⏳`:

### v1 design ⏳

Decided by the owner on 2026-10-03: every recommendation of the redesign proposals was
accepted (full text, sources, the CUDA table → HISTORY.md#redesign-proposals-2026-10-03).
Design mode: none of this exists yet. The public contract is `API.md`, `## v1 contract
⏳`; the checks, frozen before any code, are in ACCEPTANCE.md (`ACCEPTANCE.md`). Coding
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

Each one is checked by the item of the same number in ACCEPTANCE.md (`ACCEPTANCE.md`).

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

Original text of `API.md`, from `## How the package is used ✅` to the end of
`## Out of scope`:

### How the package is used ✅

```csharp
public sealed class DifferentialEvolutionOptimizer
    : IDifferentialEvolutionOptimizer<OptimizationResult>, IDisposable
{
    public DifferentialEvolutionOptimizer(IKernelController kernelController);

    public Task<OptimizationResult> RunAsync();
    public Task<OptimizationResult> RunAsync(CancellationToken cancellationToken);
    public void Dispose();
}
```

The path through the nodes:

1. The caller creates an ILGPU `Context` and `Accelerator`, allocates device buffers
   for the box bounds and for the random states (one `XorShift32` per individual,
   non-zero seeds), and writes the objective as a struct implementing
   `IFitnessFunctionInvoker` (`Interfaces/API.md`).
2. It builds the parts: `PopulationSamplingMaker` (`PopulationSamplingMakers/API.md`)
   (size and box, upper bound first), `RandomGenerator` (`RandomGenerators/API.md`),
   `MutationStrategy<RandomGenerator>` (`MutationStrategies/API.md`) (lower bound
   first), `SelectionStrategy` (`SelectionStrategies/API.md`) and
   `MaxGenerationStrategy` (`TerminationStrategies/API.md`).
3. It passes them, with the context and the device, to
   `KernelController<…>` (`Controllers/Kernels/API.md`), and the controller to this
   optimizer.
4. The **constructor** compiles the kernels and allocates the populations
   (`CompileAndGpuMemoryAlloc`); ILGPU compilation errors surface here.
5. `RunAsync` evaluates the current population, runs generations until the stop rule
   or the token, copies the whole current population to the host and returns the
   individual with the lowest fitness (the first one found on a tie) as an
   `OptimizationResult` (`Models/API.md`). It does all this **synchronously** on the
   calling thread and returns a completed task.
6. `Dispose` disposes the controller — which disposes the context and the device —
   and then calls `GC.Collect()`.

A second `RunAsync` continues from where the first stopped: it re-evaluates the current
population and runs a new full count of generations.

### Errors

| Situation | Behaviour |
|---|---|
| The kernels cannot be compiled | ILGPU's exception from the constructor |
| No current population when picking the result | `InvalidOperationException` (cannot happen after a successful constructor) |
| Cancellation | A normal result from the population reached so far; no `OperationCanceledException` |
| Individual 0 has fitness `NaN` | Returned as the best: every comparison with `NaN` is false (by reading the code) |

### Side effects

Blocks the calling thread for the whole run; copies the population to the host once
per `RunAsync`; forces a full garbage collection on `Dispose`.

### Children

- Interfaces (`Interfaces/API.md`) — the optimizer, objective and observer contracts.
- Models (`Models/API.md`) — device and host populations, result types.
- Controllers/Kernels (`Controllers/Kernels/API.md`) — kernel controller and its
  contract (`Controllers/Kernels/Interfaces/API.md`).
- MutationStrategies (`MutationStrategies/API.md`) — DE/rand/1/bin, and its
  contract (`MutationStrategies/Interfaces/API.md`).
- SelectionStrategies (`SelectionStrategies/API.md`) — greedy selection, and its
  contract (`SelectionStrategies/Interfaces/API.md`).
- RandomGenerators (`RandomGenerators/API.md`) — per-thread `XorShift32`, and its
  contract (`RandomGenerators/Interfaces/API.md`).
- PopulationSamplingMakers (`PopulationSamplingMakers/API.md`) — uniform box sampler,
  and its contract (`PopulationSamplingMakers/Interfaces/API.md`).
- TerminationStrategies (`TerminationStrategies/API.md`) — generation limit, and its
  contract (`TerminationStrategies/Interfaces/API.md`).

### Out of scope

- A builder: every part is assembled by hand.
- Seeding a run, adaptive variants (jDE, JADE, SHADE, L-SHADE), other mutation schemes,
  stop rules other than a generation count.
- `DotNetOptimization.Abstractions`: the result is not an `ISolution` and the objective
  is not an `IFitnessFunctionEvaluator`.

Original text of `ACCEPTANCE.md`, `## 0.x, the code as it stands`:

### 0.x, the code as it stands

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

<a id="redesign-proposals-2026-10-03"></a>
## 2026-10-03 — `BOOT.md`, `## Redesign proposals ⏳`, moved in full

Moved when the owner accepted every recommendation (2026-10-03) and the section was
replaced by `## v1 design ⏳`. The decisions stay in `BOOT.md`; the reasoning, the
sources and the CUDA measurement table are here. Original text:

Design mode, 2026-10-03: proposals only, no code. "Agreed in principle" means the owner
agreed on 2026-10-02 when the import was planned. **Decided 2026-10-03: the owner accepted
every recommendation below**, items 1–12 (in chat, after PR #13). Their realisation is the
v1 design; the ⏳ on this heading stays until that design is written and approved.

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

   Measured 2026-10-03 with the two `GPU.Test` cases (Rosenbrock with `Math.Pow`, the
   polynomial fit with `XMath.Pow`) in a scratch copy, on the device the probe asserted:
   - ILGPU 1.5.1, CUDA, RTX 5070 Ti (compute 12.0, driver 616.92): both fail with
     `CudaException: a PTX JIT compilation failed`. **The released package cannot run
     on this GPU through CUDA.**
   - ILGPU 1.5.3, CUDA, same GPU: 2 of 2 pass (29 s for the run, compile included).
   - ILGPU 1.5.3, OpenCL, `gfx1036`: 2 of 2 pass (50 s), so the upgrade breaks nothing
     there.

   Consequence for v1: ILGPU 1.5.3. APT's libdevice gap was not seen here. Whether these
   kernels used libdevice at all was not established, so the probe kernel APT uses (a
   math kernel that must load before CUDA counts as bound) stays part of the design.
   The timings are one run each, compile included: not a speed comparison.
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
