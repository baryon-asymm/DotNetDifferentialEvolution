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

- [x] **1a, initial sampling.** Seeded, N = 1000, D = 3, box [−2, 5]:
      - every gene is in [lower, upper);
      - per gene, a χ² test on 20 bins stays under the 0.999 quantile;
      - the evaluation count after `Build` is N.
      Red: the span written as `upper` instead of `upper − lower`.
      2026-10-03: `Builder/InitialSamplingTests` (the package's init kernel through
      `KernelLauncher` on the CPU accelerator), green: χ² 21.52, 19.96, 20.60 against the computed
      quantile 43.8202 (df 19); the count after `Build` is N. Red with the span written as `upper`
      (gene 0: χ² 399.92). In [lower, upper) by the sample: `lower + u·(upper − lower)` with
      u ≤ 1 − 2⁻⁵³ can still round to `upper` for some boxes, as the CPU package's sampler can;
      not observed in the 3 000 genes.
- [x] **1b, index draws.** On the CPU accelerator, for N = 4 and N = 50, with 10⁵ draws
      per i:
      - r1, r2 and r3 are mutually distinct and never equal i;
      - each admissible index occurs uniformly (χ², 0.999).
      Red: the "skip i" step removed.
      ⚠ 2026-10-03, read as: one joint χ² per (N, role) over every cell (i, index ≠ i),
      df = N·(N − 2), so 8 for N = 4 and 2400 for N = 50, six tests at the 0.999 quantile. A
      separate test per (N, i, role) would be 162 tests at 0.999, failing by chance about one
      run in seven; the reasoning is in `GPU.Test/Kernels/BOOT.md`.
      2026-10-03: `DonorPickTests` (test kernel `DonorPickKernel` on the CPU accelerator), green;
      red with the skip-i step removed (300 000 and 300 376 violations).
- [x] **1c, binomial crossover with `jrand`**, on scripted draws:
      - CR = 0 takes exactly gene `jrand` from the mutant;
      - CR = 1 takes every gene;
      - a scripted mix matches the closed form gene by gene.
      Red: `|| j == jrand` removed (the CR = 0 case).
      2026-10-03: `CrossoverTests`, green; red with `|| j == jrand` removed (the CR = 0
      case: [10, 20, 30, 40, 50] against [10, 20, 4, 40, 50]).
- [x] **1d, midpoint repair**, on scripted draws:
      - a gene below the bound becomes exactly `(lower + x) / 2`;
      - a gene above it becomes `(upper + x) / 2`;
      - an in-box mutant gene is untouched.
      Red: a clamp to the bound.
      2026-10-03: `RepairTests`, green (−0.5 below, 10.5 above, in-box genes untouched,
      one on the bound); red with a clamp to the bound.
- [x] **1e, selection.** The nine cases of the CPU package's `SelectionStrategyTests`
      give the same survivor: better, worse, tie, and `NaN` in the parent, the trial
      and both.
      Red: `<=` written as `<` (the tie case); `NaN` handled by `<` alone (the
      `NaN`-parent case).
      ⚠ 2026-10-03, changed openly: eight cases, not nine. Three of the CPU package's
      nine test its `WithTiesRejected` mode, which the GPU package does not have; two of those
      have the same survivor under the GPU rule (strictly better, `NaN` parent) and are tested;
      "ties rejected keeps the parent on a tie" has no GPU counterpart and is not.
      2026-10-03: `SurvivalTests`, green; red with `<=` written as `<` (the tie case), and with
      `NaN` left to `<=` alone (both `NaN`-parent cases); `<` alone fails all three.
- [x] **1f, best pick.** Fitness `[NaN, 3, 1, 1]` gives index 2.
      Red: a plain `<` scan from index 0, which returns 0.
      2026-10-03: `BestPickTests`, green; red with the `NaN` clause removed (index 0).
- [x] **1g, parity with the CPU package.** The same scripted draws give bit-identical
      trial vectors through the GPU DE step and through the CPU
      `CrossoverHelper.BinomialCrossoverAndRepair` (100 random cases, fixed seed). The
      test project references both packages; neither package references the other.
      Red: the GPU repair changed to a clamp.
      2026-10-03: `CpuParityTests`, 100 cases (seed 20261003; N 4–40, D 1–12, F 0.1–2,
      CR 0 and 1 included): draw kinds, ranges and counts equal one to one, trials bit-identical,
      repairs below and above the box both occurring; red with the GPU repair as a clamp.
- [x] **1h, convergence to a known optimum** (positive control). Seed 1, on the CPU
      accelerator, and again under **Gpu** on both devices:
      - Sphere 5-D reaches 1e-6;
      - Rosenbrock 2-D reaches 1e-6, with the genes within 1e-3 of (1, 1);
      - Rastrigin 2-D reaches 1e-4.
      Red: selection that never takes the trial (all three red).
      2026-10-03: `EndToEnd/ConvergenceTests` (N 50, F 0.5, CR 0.9, 1 000 generations,
      chosen before the first run), green on the CPU accelerator and, under **Gpu**, on CUDA and
      OpenCL: Sphere ≤ 2e-88, Rosenbrock exactly 0 at (1, 1), Rastrigin exactly 0 (its cosine a
      series of its own, `Math.Cos` being off the allow-list). Red with `Survives` never true: all
      nine cases.

### 2. Race-freedom held by the kernel

- [x] **2a.** `GeneView` exposes no writable member: no setter, no `ref` return, no
      public field (reflection).
      Red: a public setter on the indexer.
      2026-10-03: `GeneViewSurfaceTests` (reflection over every member), green; red with
      a public setter on the indexer.
- [x] **2b.** After one generation (N = 64, D = 4, fixed seed), every slot i of the next
      population equals either parent i or trial i recomputed on the host from the same
      draws.
      Red: the kernel writing next slot `(i + 1) % N`.
      2026-10-03: `GenerationSlotTests` (N 64, D 4), green, both outcomes occurring; red
      with the kernel writing next slot `(i + 1) % N`.

### 3. The RNG

- [x] **3a, Philox4x32-10 known answers.** The three vectors of Random123's
      `kat_vectors` (zero, all-ones, π digits; the test cites the source) match:
      - on the host;
      - inside a kernel on the CPU accelerator;
      - under **Gpu**, inside a kernel on each device.
      Red: one round constant changed.
      2026-10-03: `PhiloxKnownAnswerTests` (host, and a kernel on the CPU accelerator) and,
      under **Gpu**, `DeviceDrawTests.AKernelOnTheDeviceGivesTheKnownAnswers` on CUDA (RTX 5070 Ti)
      and OpenCL (`gfx1036`); the vectors cite Random123 `tests/kat_vectors` at 9545ff6, file
      SHA-256 aab5ebab…86929183, lines 27–29. Red with `Multiplier0` 0xD2511F53 → 0xD2511F57:
      all six fail.
- [x] **3b, uniform doubles.**
      - 10⁶ draws: a χ² test on 100 bins stays under the 0.999 quantile.
      - The largest value the 53-bit construction can give, computed exactly, is < 1.
      Red: 32 bits used instead of 53 (caught by the analytic check of the maximum).
      2026-10-03: `UnitDoubleTests`, green; the quantile is computed (`ChiSquared`,
      incomplete gamma and bisection) and checked by `ChiSquaredTests` against closed forms,
      df = 2 to 1e-12. Red with 32 bits: the analytic maximum fails (0.99999999976716936), the
      χ² alone stays green, as the check foresaw.
- [x] **3c, index draws.** Lemire multiply-shift gives exactly the closed-form output on
      scripted 32-bit words.
      Red: a modulo reduction instead.
      2026-10-03: `IndexDrawTests` (16 words × 11 values of n, against `BigInteger`),
      green; red with a modulo reduction.

### 4. Reproducibility

- [x] **4a.** The same seed, run twice on the CPU accelerator, gives bit-identical genes
      and fitness, and seeds 1 and 2 differ. Under **Gpu**, the same on each device.
      Red: a seed taken from `Random.Shared` even when one is given.
      2026-10-03: `EndToEnd/ReproducibilityTests` (population and result compared as bit
      patterns), green on the CPU accelerator and, under **Gpu**, on CUDA and OpenCL. Red with the
      seed drawn even when given, on all three. (The code draws an unseeded seed from
      `RandomNumberGenerator`, not `Random.Shared`: HISTORY.md#v1-built-2026-10-03, item 2.)
- [x] **4b.** The first 10⁴ draws of a fixed (seed, individual, generation) are
      bit-identical on the CPU accelerator and, under **Gpu**, on CUDA and OpenCL.
      Results across backends are not compared bitwise (`BOOT.md`, invariant 4).
      2026-10-03: `DrawSequenceTests` on the CPU accelerator and, under **Gpu**,
      `DeviceDrawTests.TheDeviceDrawsTheHostWords` on CUDA and OpenCL: 10⁴ words equal to the
      host's; another seed, individual or generation gives another sequence.

### 5. No per-generation host round trip

- [x] **5a.** A fact in `Protocol.Tests`: in the package, only the methods of one
      transfer helper call an ILGPU host transfer.
      Red: a `CopyToCPU` added in the generation loop.
      2026-10-03: `GpuGuardTests.OnlyTheTransferHelperCallsAnIlgpuHostTransfer`, green
      (three transfers found, all in `PopulationTransfers`); red on a `CopyToCPU` after
      the swap in `GpuDifferentialEvolution.Run`; fails "found nothing" on an empty scan.
- [x] **5b.** That helper counts its calls:
      - a 100-generation run with no observer makes exactly one download, the final
        population;
      - with an observer every 10 generations, it makes 11.
      Red: a download per generation.
      2026-10-03: `EndToEnd/TransferCountTests`, green (1 and 11); red with a download per
      generation.

### 6. Asynchrony and cancellation

- [x] **6a.** While an observer is blocked at generation 1 on a gate the test holds,
      `RunAsync` has already returned an incomplete task. Deterministic, no timing.
      Red: the loop run on the caller's thread.
      2026-10-03: `EndToEnd/AsynchronyTests.RunAsyncReturnsAnIncompleteTaskWhileTheObserverIsHeld`,
      green; red with the loop run on the caller's thread.
- [x] **6b.** A token cancelled from the observer at generation 3 ends the task as
      canceled, after at most 4 generations.
      2026-10-03: `ATokenCancelledAtGenerationThreeEndsTheTaskAsCanceled` (canceled, 3 to 4
      observer calls), green; red with the token not observed in the loop.
- [x] **6c.** After a run, a second `RunAsync` returns the same task; during a run it
      throws `InvalidOperationException`.
      2026-10-03: `ASecondCallAfterTheRunReturnsTheSameTask`, `ACallDuringTheRunThrows`,
      green; red with the finished task not reused.

### 7. Ownership

- [x] **7a.** A forbidden-call rule in `Protocol.Tests`: no `GC.Collect` anywhere in the
      package.
      Red: the 0.x `Dispose`.
      2026-10-03: the rule "no GC.Collect in the GPU package" in
      `ProtocolConfig.ForbiddenCallRules`, run by `ForbiddenCallTests`; green; red on
      `GC.Collect()` added to `Dispose`.
- [x] **7b.** A caller-owned accelerator still allocates a buffer after the optimizer's
      `Dispose`.
      Red: disposing it.
      2026-10-03: `EndToEnd/OwnershipTests`, the CPU accelerator and, under **Gpu**, CUDA and
      OpenCL. Stricter than written: ILGPU's CPU accelerator still allocates after its own
      `Dispose`, so the test also launches a kernel into the buffer and reads it back. Red with the
      lease always disposing: `ObjectDisposedException` on the CPU accelerator, `CudaException`,
      `CLException`.

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

- [x] **D1.** On a machine with no GPU (hosted CI):
      - `Auto` gives `Cpu`, with a non-null `FallbackReason`;
      - an explicit `Cuda` throws `InvalidOperationException` naming CUDA.
      Under **Gpu**, an explicit `Cuda` gives the RTX 5070 Ti and `OpenCL` gives
      `gfx1036`.
      2026-10-03: `Devices/DeviceSelectionTests`, each case asking ILGPU which devices are
      present and asserting its branch; all branches run here, the no-GPU ones with the GPUs hidden
      (`CUDA_VISIBLE_DEVICES=-1`, `GPU_DEVICE_ORDINAL`): Auto then gives `Cpu` with "CUDA: no such
      device is present.; OpenCL: no such device is present." Under **Gpu**: "NVIDIA GeForce RTX
      5070 Ti" and `gfx1036`. Red: an explicit device falling back, a message without the device's
      name, a null reason, CUDA mapped to OpenCL. Not yet run on a hosted runner.
- [ ] **D2.** The CUDA math probe, under **Gpu**: `Exp`, `Log`, `Pow` and `Sqrt` in a
      kernel agree with `System.Math` within 4 ULP on 10⁴ arguments. The tolerance is
      APT's measurement for libdevice, not one chosen here.
      ⚠ 2026-10-03: **red on CUDA, stopped, the owner's decision.** `Devices/MathProbeTests`,
      10⁴ arguments log-spaced in [1e-3, 700], RTX 5070 Ti, contexts with `EnableAlgorithms()`:
      | function | CUDA max ULP (at x) | arguments over 4 ULP | OpenCL `gfx1036` max ULP |
      |---|---|---|---|
      | Exp | 195 (652.2457760772028) | 2 618 | 1 |
      | Log | 9 430 (0.9999920999476787) | 520 | 1 |
      | Pow(x, 1.37) | 24 (0.0013455021986893204) | 2 807 | 1 |
      | Sqrt | 0 | 0 | 0 |
      The 4-ULP bound is APT's measurement for libdevice; ILGPU.Algorithms computes these in
      software (APT root `BOOT.md`: "Algorithms replaces double math with CORDIC"), and
      ILGPU 1.5.3's own libdevice path is defective for compute 10.0 and newer, where APT
      completes it with a post-link of its own and needs a CUDA Toolkit 12.8+ installed. The
      package's kernels call none of the three; only a caller's objective can.
- [x] **B1.** Every row of `API.md`'s v1 error table has a test that triggers it.
      2026-10-03: `Builder/BuilderErrorTests` (every argument row, each with a passing
      boundary case beside it; the foreign accelerator built by hand, type 99),
      `EndToEnd/RunErrorTests` (the observer's exception faults the task, same instance),
      `AsynchronyTests` (a call during the run), `Devices` (an absent device, conditional like D1).
      An objective with a `throw` fails `Build` with ILGPU's `InternalCompilerException` on all
      three backends, so that row runs in CI. Red: each guard weakened in turn.
- [x] **R1.** CI packs the GPU package from the build, without publishing, like the CPU
      "Pack" step. `release.yml` publishes it only from a `gpu-v*` tag; a `v*` tag does
      not pack it.

      2026-10-03, by reading and a local pack: `ci.yml` packs the GPU package after the
      build without publishing; `release.yml` has a `publish-gpu` job that runs only for
      `gpu-v*` and a `publish-cpu` job only for `v*`. A local `dotnet pack` gives the DLL, the XML
      documentation, `README.md`, `LICENSE` and `ILGPU_LICENSE`. Not yet run on GitHub.