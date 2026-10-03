# ACCEPTANCE.md — DotNetDifferentialEvolution.GPU

The node's acceptance criteria (AGENTS.md 3.2, §6): the checks of v1, frozen on 2026-10-03
before any v1 code, and the state of each.

The criteria of 0.x left with its code on 2026-10-03 → HISTORY.md#v1-built-2026-10-03.

## v1 — checks frozen before code ⏳

Written 2026-10-03, before any v1 code.

- **Proven twice.** Each check is green on an input with a known answer, and red on the
  named mutation, applied once to a scratch copy and never committed.
- **Frozen.** A check is changed only openly, with ⚠ and the reason. Any red frozen
  check stops the slice; nothing is loosened to pass (the skill's `LESSONS.md`).
- **Where they run.** Unless marked **Gpu**, a check runs on ILGPU's CPU accelerator in
  hosted CI. A **Gpu** check runs locally on CUDA (RTX 5070 Ti) and OpenCL (`gfx1036`)
  and is excluded from CI.
- The numbers follow the invariants of `BOOT.md`.

### 1. Semantics (`docs/ALGORITHMS.md` §§2–3, §9)

- [ ] **1a, initial sampling.** Seeded, N = 1000, D = 3, box [−2, 5]:
      - every gene is in [lower, upper);
      - per gene, a χ² test on 20 bins stays under the 0.999 quantile;
      - the evaluation count after `Build` is N.
      Red: the span written as `upper` instead of `upper − lower`.
- [ ] **1b, index draws.** On the CPU accelerator, for N = 4 and N = 50, with 10⁵ draws
      per i:
      - r1, r2 and r3 are mutually distinct and never equal i;
      - each admissible index occurs uniformly (χ², 0.999).
      Red: the "skip i" step removed.
- [ ] **1c, binomial crossover with `jrand`**, on scripted draws:
      - CR = 0 takes exactly gene `jrand` from the mutant;
      - CR = 1 takes every gene;
      - a scripted mix matches the closed form gene by gene.
      Red: `|| j == jrand` removed (the CR = 0 case).
- [ ] **1d, midpoint repair**, on scripted draws:
      - a gene below the bound becomes exactly `(lower + x) / 2`;
      - a gene above it becomes `(upper + x) / 2`;
      - an in-box mutant gene is untouched.
      Red: a clamp to the bound.
- [ ] **1e, selection.** The nine cases of the CPU package's `SelectionStrategyTests`
      give the same survivor: better, worse, tie, and `NaN` in the parent, the trial
      and both.
      Red: `<=` written as `<` (the tie case); `NaN` handled by `<` alone (the
      `NaN`-parent case).
- [ ] **1f, best pick.** Fitness `[NaN, 3, 1, 1]` gives index 2.
      Red: a plain `<` scan from index 0, which returns 0.
- [ ] **1g, parity with the CPU package.** The same scripted draws give bit-identical
      trial vectors through the GPU DE step and through the CPU
      `CrossoverHelper.BinomialCrossoverAndRepair` (100 random cases, fixed seed). The
      test project references both packages; neither package references the other.
      Red: the GPU repair changed to a clamp.
- [ ] **1h, convergence to a known optimum** (positive control). Seed 1, on the CPU
      accelerator, and again under **Gpu** on both devices:
      - Sphere 5-D reaches 1e-6;
      - Rosenbrock 2-D reaches 1e-6, with the genes within 1e-3 of (1, 1);
      - Rastrigin 2-D reaches 1e-4.
      Red: selection that never takes the trial (all three red).

### 2. Race-freedom held by the kernel

- [ ] **2a.** `GeneView` exposes no writable member: no setter, no `ref` return, no
      public field (reflection).
      Red: a public setter on the indexer.
- [ ] **2b.** After one generation (N = 64, D = 4, fixed seed), every slot i of the next
      population equals either parent i or trial i recomputed on the host from the same
      draws.
      Red: the kernel writing next slot `(i + 1) % N`.

### 3. The RNG

- [ ] **3a, Philox4x32-10 known answers.** The three vectors of Random123's
      `kat_vectors` (zero, all-ones, π digits; the test cites the source) match:
      - on the host;
      - inside a kernel on the CPU accelerator;
      - under **Gpu**, inside a kernel on each device.
      Red: one round constant changed.
- [ ] **3b, uniform doubles.**
      - 10⁶ draws: a χ² test on 100 bins stays under the 0.999 quantile.
      - The largest value the 53-bit construction can give, computed exactly, is < 1.
      Red: 32 bits used instead of 53 (caught by the analytic check of the maximum).
- [ ] **3c, index draws.** Lemire multiply-shift gives exactly the closed-form output on
      scripted 32-bit words.
      Red: a modulo reduction instead.

### 4. Reproducibility

- [ ] **4a.** The same seed, run twice on the CPU accelerator, gives bit-identical genes
      and fitness, and seeds 1 and 2 differ. Under **Gpu**, the same on each device.
      Red: a seed taken from `Random.Shared` even when one is given.
- [ ] **4b.** The first 10⁴ draws of a fixed (seed, individual, generation) are
      bit-identical on the CPU accelerator and, under **Gpu**, on CUDA and OpenCL.
      Results across backends are not compared bitwise (`BOOT.md`, invariant 4).

### 5. No per-generation host round trip

- [x] **5a.** A fact in `Protocol.Tests`: in the package, only the methods of one
      transfer helper call an ILGPU host transfer.
      Red: a `CopyToCPU` added in the generation loop.
      2026-10-03: `GpuGuardTests.OnlyTheTransferHelperCallsAnIlgpuHostTransfer`, green
      (three transfers found, all in `PopulationTransfers`); red on a `CopyToCPU` after
      the swap in `GpuDifferentialEvolution.Run`; fails "found nothing" on an empty scan.
- [ ] **5b.** That helper counts its calls:
      - a 100-generation run with no observer makes exactly one download, the final
        population;
      - with an observer every 10 generations, it makes 11.
      Red: a download per generation.

### 6. Asynchrony and cancellation

- [ ] **6a.** While an observer is blocked at generation 1 on a gate the test holds,
      `RunAsync` has already returned an incomplete task. Deterministic, no timing.
      Red: the loop run on the caller's thread.
- [ ] **6b.** A token cancelled from the observer at generation 3 ends the task as
      canceled, after at most 4 generations.
- [ ] **6c.** After a run, a second `RunAsync` returns the same task; during a run it
      throws `InvalidOperationException`.

### 7. Ownership

- [x] **7a.** A forbidden-call rule in `Protocol.Tests`: no `GC.Collect` anywhere in the
      package.
      Red: the 0.x `Dispose`.
      2026-10-03: the rule "no GC.Collect in the GPU package" in
      `ProtocolConfig.ForbiddenCallRules`, run by `ForbiddenCallTests`; green; red on
      `GC.Collect()` added to `Dispose`.
- [ ] **7b.** A caller-owned accelerator still allocates a buffer after the optimizer's
      `Dispose`.
      Red: disposing it.

### 8. Kernel guards (facts in `Protocol.Tests`, adapted from APT, source cited)

- [x] **8a.** The IL reachable from each kernel entry point contains no `throw`,
      `newarr`, `newobj` or `box`.
      Red: a `throw` in the DE step.
      ⚠ 2026-10-03, changed openly: `newobj` is refused for reference types only. A
      value-type `newobj` is a construction on the stack, which ILGPU compiles; the
      kernels build `PhiloxBlock` and `GeneView` that way and run on all three backends
      (the smoke run of HISTORY.md#v1-built-2026-10-03). The literal wording would fail
      every kernel. APT's guard draws the same line.
      2026-10-03: `GpuGuardTests.KernelReachableCodeNeitherThrowsNorAllocatesNorBoxes`,
      green over the five entry points and 39 reached methods; red on a `throw` in
      `DeStep.BuildTrial`, and on one in `PhiloxDraws.NextIndex`, reached only through
      `IDrawSource`. Limit: the walk reads IL of the tree only, so a BCL throw helper
      (`ArgumentOutOfRangeException.ThrowIfNegative`) called from kernel code is not
      seen.
- [x] **8b.** Kernel code calls only allow-listed `Math` members: `Abs`, `Sqrt`, `Exp`,
      `Log`, `Pow`, `Floor`, `Min`, `Max`, `IsNaN`.
      Red: `Math.Cbrt` in a kernel.
      2026-10-03: `GpuGuardTests.KernelReachableCodeCallsOnlyTheAllowedMathAndDoubleMembers`,
      green (Exp, Log, Pow, Sqrt, IsNaN found); red on `Math.Cbrt` in `MathProbe.Probe`.
      By name, as written: an integer overload of an allowed name passes.
- [x] **8c.** No constant on the left of an ordered floating-point comparison anywhere in
      the package.
      Red: `0.0 < x`.
      2026-10-03: `GpuGuardTests.GpuSourcesPutNoConstantLeftOfAnOrderedFloatingComparison`
      (Roslyn semantic model, Microsoft.CodeAnalysis.CSharp 5.0.0), green over 33 sources
      and 8 ordered floating comparisons; red on `0.0 < inputs[i]` in `MathProbe.Probe`.
- [x] **8d.** Host transfers only through the pinning (`Span`/array) overloads.
      Red: a `CopyToCPU(ref …)`.
      2026-10-03: `GpuGuardTests.NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference`,
      green; red on `CopyToCPU(ref hostFitness[0], …)` in `PopulationTransfers.Download`.

### Devices, builder, release

- [ ] **D1.** On a machine with no GPU (hosted CI):
      - `Auto` gives `Cpu`, with a non-null `FallbackReason`;
      - an explicit `Cuda` throws `InvalidOperationException` naming CUDA.
      Under **Gpu**, an explicit `Cuda` gives the RTX 5070 Ti and `OpenCL` gives
      `gfx1036`.
- [ ] **D2.** The CUDA math probe, under **Gpu**: `Exp`, `Log`, `Pow` and `Sqrt` in a
      kernel agree with `System.Math` within 4 ULP on 10⁴ arguments. The tolerance is
      APT's measurement for libdevice, not one chosen here.
- [ ] **B1.** Every row of `API.md`'s v1 error table has a test that triggers it.
- [ ] **R1.** CI packs the GPU package from the build, without publishing, like the CPU
      "Pack" step. `release.yml` publishes it only from a `gpu-v*` tag; a `v*` tag does
      not pack it.
