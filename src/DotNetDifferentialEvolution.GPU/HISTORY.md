# HISTORY.md — DotNetDifferentialEvolution.GPU

Append-only, newest first (AGENTS.md §15). Read by following a pointer, not at start.

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
