# ACCEPTANCE.md — DotNetDifferentialEvolution.GPU/Kernels

The node's acceptance criteria (AGENTS.md 3.2, §6). Its checks from 2026-10-03 and
2026-10-05 — 1b–1g, 2b, 8a–8c, S2–S6 and S16 — stay in the package's
[ACCEPTANCE.md](../ACCEPTANCE.md), whose file is at its limit (§15). The pointwise
checks P0–P5 are here, under that file's rules (proven twice: green on a known answer and
red once on the named mutation, applied to a scratch copy and never committed; frozen; CI
unless marked **Gpu**). P2 holds the package root's builder, whose file is full.

## The pointwise objective — checks P0–P5, frozen 2026-10-09, before code

- [x] **P0, the single-kernel path unchanged.** At the base commit, before any code
      changes, a test runs each of the nine configurations of check S13 (Sphere D = 10 in
      [−5, 5], N = 100, L-SHADE N_init 180, seed 1, 2·10⁴ evaluations) on the CPU
      accelerator and takes one SHA-256 over, configuration by configuration in S13's
      order: the best genes and the fitness as IEEE-754 bits, the generation and the
      evaluation counts. The hash measured at the base is written into the test and
      committed before the refactoring (green by construction); the test stays green after
      it. Red: jDE's F and CR handed to the individual when its parent is kept.
      2026-10-09, local, Release: `EndToEnd/SingleKernelPathTests`, hash `CF28D8AE…ECF91`
      measured at the test-only commit 0d26bc8, green there, after the extraction and at
      a5bbd8d; red under the named mutation (orchestrator's rerun).
- [x] **P1, the same run as a monolithic objective.** A pointwise objective and its
      monolithic twin, the same arithmetic in the same order, give the same run for each
      of the nine configurations: the best genes, the fitness, the generations, the
      evaluations and every population snapshot (observer every 25 generations), bit for
      bit. The pair: D = 10 in [−5, 5]; P = 12 points in 3 groups of 4; point p returns a
      struct of a `double` residual `(Σ_j a_pj·x_j − b_p)²` with fixed coefficients and an
      `int` flag (0 when the residual exceeds a fixed threshold); `Combine` takes, group
      by group in point order, the square root of the group's mean residual, adds the
      three, and adds 1 per flagged point; the twin's `Evaluate` does the same in one
      thread. N = 64 (L-SHADE N_init 64), seed 1, 2·10⁴ evaluations; and SHADE with the
      stagnation rule (max streak 5, threshold 0) on a stepped objective of the same
      shape, so that a run stops by the stop word. On the CPU accelerator (CI) and on CUDA
      (**Gpu**). Red, each once: the point kernel mapping thread k to point `k div N` of
      individual `k mod N`; `Combine` given the next individual's results; a surviving
      trial not handing jDE its F and CR in the pointwise path; all three generation
      kernels ignoring the stop word, in a run whose word is read only after 200
      generations (S18's construction). ⚠ Changed 2026-10-09 after code: the frozen red was
      the select kernel alone, which cannot turn red — with no new trial and no new
      results it re-selects a survivor against the same trial, a fixed point (measured
      11 of 11 green). Build and select without the check (11 of 11 green) put new trials
      beside stale results, which only the post-stop population would show, and no snapshot
      can be taken there: an observer reads the word first (S17). A known gap.
      ⚠ 2026-10-10: the CUDA leg holds for arithmetic the device compiler leaves
      uncontracted. ptxas fuses a monolithic `sum += r·r` into one DFMA, which the pointwise
      form, storing `r·r`, cannot: a least-squares and a Sphere pair differed in 19 and 21 of
      64 snapshot fitness values by 1–2 ulp (measured 2026-10-09, RTX 5070 Ti). This pair is
      not contracted; the promise is now "bit for bit on the CPU accelerator"
      ([HISTORY.md](../HISTORY.md#audit-fixes-decided-2026-10-10)).
      2026-10-09, local, Release: `EndToEnd/PointwiseEquivalenceTests`, 11 of 11 on the
      CPU accelerator and 11 of 11 on CUDA (RTX 5070 Ti): best genes, fitness, generations,
      evaluations and 12 snapshot hashes (L-SHADE 924 generations, 36) equal the twin's;
      stagnation and stop-word runs end at generation 11, 768 evaluations. Reds, rerun by
      the orchestrator: the `k div N` mapping 11 of 11; the next individual's results
      11 of 11; jDE without the trial's F and CR, the jDE case (`40217159A3C2E618` against
      `40214E9E15F21D39`); all three kernels past the stop, the stop-word case (fitness
      bits `4026000000000000` against `4022000000000000`).
- [x] **P2, the builder.** `ForPointwiseFunction(f, 0)` and `(f, −1)` throw
      `ArgumentOutOfRangeException`; `WithPopulationSize` refuses `N·P > int.MaxValue`
      (N = 2²⁰, P = 2¹²) with the exception it throws for `N·D`; the existing
      `ForFunction` tests compile and pass unchanged. Red: `P = 0` accepted.
      2026-10-09, local, Release: `EndToEnd/PointwiseBuilderTests`, 5 of 5 (also the exact
      edge, N·P = 2³¹ refused, 2³¹ − 2¹⁰ accepted); the GPU suite's 320 earlier tests
      unchanged. Red: `ThrowIfLessThan(pointCount, 0)`, "No exception was thrown".
- [x] **P3, data and math on CUDA** (**Gpu**). A pointwise objective that carries an
      `ArrayView<double>` field and calls `Exp` and `Pow` in `EvaluatePoint`, with a
      result struct of two `double`s and an `int`, builds and runs on CUDA and equals its
      monolithic twin bit for bit (L-SHADE, N_init 1 024, P = 50, seed 1, 50 generations).
      Red: the pointwise kernels loaded without `KernelLoader` (no libdevice post-link):
      `Build` throws.
      2026-10-09, local, Release, CUDA (RTX 5070 Ti): `EndToEnd/PointwiseCudaMathTests`,
      34.472584015541244 after 50 generations and 40 885 evaluations, every snapshot equal
      to the twin's. Red: `LoadAutoGroupedKernel` in place of `KernelLoader.Load`, `Build`
      throws `CudaException` "a PTX JIT compilation failed" (seen beneath ILGPU's
      "invalid resource handle" from the accelerator's `Dispose`, which masks it).
- [x] **P4, the point of it: latency** (**Gpu**). An objective of P = 50 points, each 40
      rounds of `Exp` and `Pow` on doubles, under DE/rand/1/bin on CUDA: per generation,
      the median of three timed batches of 50 generations after one warm-up batch, at
      N = 1 024 and N = 16 384, pointwise against its monolithic twin. Pass: at
      N = 1 024 the pointwise generation is at least 4× faster; both figures at both N
      are recorded. Red: the point kernel launched with N threads, each looping over its
      `P` points.
      2026-10-09, local, Release, CUDA (RTX 5070 Ti), two runs, ms per generation,
      monolithic / pointwise: N = 1 024 — 28.98 / 0.772 (37.5×), 28.49 / 0.748 (38.1×);
      N = 16 384 — 28.76 / 11.15 (2.58×), 28.50 / 11.15 (2.56×). Red: one thread per
      individual looping over its points, 28.59 / 34.21 (0.84×) at N = 1 024.
- [x] **P5, the kernel guards cover the new kernels.** Checks 8a–8c find kernel entry
      points by their first parameter (`Index1D`), so the pointwise kernels are guarded
      without registration. Red: a `throw` in `EvaluatePoints` turns 8a red.
      2026-10-09, local, Release: `Protocol.Tests/GpuGuardTests` green over the new
      kernels; with the `throw`, 8a red naming `PointwiseKernels.EvaluatePoints` for the
      `throw` and the `newobj` of `InvalidOperationException` (orchestrator's rerun).

## Audit fixes — checks A5–A13, frozen 2026-10-10, before code

From the audits of 2026-10-09 ([HISTORY.md](../HISTORY.md#audit-fixes-decided-2026-10-10)).
They hold the package root (its file is full), as P2 does; A1–A2 are in
[Devices](../Devices/ACCEPTANCE.md), A3–A4 in [Bookkeeping](../Bookkeeping/ACCEPTANCE.md).
A seam a check needs is internal to the assembly and named in its test node's `API.md`.

- [ ] **A5, the point type is checked** (MEM-3, MEM-4). `ForPointwiseFunction` accepts a
      `TPoint` of sequential layout whose fields are `byte`, `sbyte`, `short`, `ushort`,
      `int`, `uint`, `long`, `ulong`, `float`, `double`, enums of them, or structs that pass
      the same rule, and whose size is its natural (unpacked) size: `double`, `int`, a record
      struct of a `double` and an `int`, a struct nesting two such, a struct with an `int`
      enum, and the point types of P1, P3 and P4. It throws `ArgumentException` (ParamName
      `TPoint`, the message naming the type and the field) for a `bool` field, a `char`
      field, `Pack = 1 {byte; double}`, `Pack = 4 {int; double}`, `LayoutKind.Auto`,
      `LayoutKind.Explicit`, and a struct nesting a refused one. Red: the check removed
      (`Pack = 1` accepted; at `c40868e` its run wrote 1 667 times out of bounds on CUDA).
- [ ] **A6, a failing release stops nothing** (MEM-2). With a release that throws planted on
      the optimizer's accelerator (`OnDevice(Cpu)`; a child whose release throws, as ILGPU's
      half-built `CudaKernel` does): (a) `Dispose` still releases every other buffer and
      kernel and the owned context, then throws `AggregateException` holding the failure; a
      second `Dispose` does nothing; (b) a `Build` that fails on an objective ILGPU cannot
      compile throws that compile exception itself, with the release failure in its
      `Data["DotNetDifferentialEvolution.GPU.ReleaseFailures"]`; (c) `Dispose` from the
      observer: the process lives and the task faults with the `AggregateException`. Red,
      each alone: a release loop without its per-item `try`; `AcceleratorLease.Dispose`
      without its `finally`; the constructor's `catch` letting the release failure replace
      the original; the run thread's `finally` letting it escape (the process dies).
- [ ] **A7, `Dispose` frees and stops** (TEST-2). On the CPU accelerator, every buffer and
      kernel the optimizer allocated is disposed (`IsDisposed`): (a) after `Dispose` while an
      observer holds the run at generation 3 — the task ends canceled; (b) after `Dispose`
      called from the observer, once the task has ended; (c) after a normal run and
      `Dispose`; a caller's accelerator stays usable in each. Red, each alone: `Dispose`
      not freeing the buffers; `Dispose` not canceling the run (the gate's wait ends under
      `HangGuard`); nothing released after an observer's `Dispose` (2026-10-09: the three
      together left 228 of 228 tests green).
- [ ] **A8, the run thread and a second `Dispose`** (MEM-7, MEM-8). An exception of any type
      thrown on the run's thread, an `OutOfMemoryException` from the observer included,
      faults the task with it and the process lives. Two threads calling `Dispose` during a
      run: neither returns before the run has stopped and A7's buffers are disposed. Red:
      the run thread catching only what it catches at `c40868e`; the second `Dispose`
      returning at once.
- [ ] **A9, sizes a kernel can index** (PERF-4, MEM-6). `WithPopulationSize` refuses
      N > `int.MaxValue − 1 023`, and for a pointwise objective N·P above it, with the
      exception it throws for N·D: N = 4, P = 536 870 911 refused; N·P = 2³¹ − 2¹⁰ accepted
      (P2's edge). `Build` checks N·D and N·P again, so a second `WithBounds` on a retained
      stage cannot pass them (`InvalidOperationException`). JADE, SHADE and L-SHADE refuse
      N > 2³⁰ at `Build` (`InvalidOperationException` naming the ranking's limit). Red:
      `Build`'s re-check removed.
- [ ] **A10, kernels compiled in `Build`, once** (DOC-1, PERF-11). For JADE, SHADE and
      L-SHADE with a stagnation limit, and a pointwise SHADE run, `KernelLoader.LoadCount`
      after `Build` has grown by the number of distinct kernels of the configuration, and
      `RunAsync` adds none. Red: the bookkeeping's lazy loads restored.
- [ ] **A11, the thread's binding restored** (MEM-5). After `Build` and `Dispose` on one
      thread (`OnDevice(Cpu)`), that thread's `Accelerator.Current` is what it was before
      `Build` (none in the test). Red: the binding left in place.
- [x] **A12, the CI filter opens no device** (TEST-1). On the owner's machine (CUDA and
      OpenCL present) a run of `Category!=Gpu&Category!=Slow` over the solution shows no
      test process in `nvidia-smi --query-compute-apps`, sampled every 0.2 s through the
      run; tests that select a device run on the CPU accelerator or carry `Category=Gpu`
      (the README's quick start runs verbatim under **Gpu**, and on `GpuDevice.Cpu` in CI).
      **Gpu**, run by hand. Red: `DocumentedExampleTests` tagged `Integration` again (an
      N = 10 000 run on CUDA, 2026-10-09).
      2026-10-10, local, RTX 5070 Ti: at `3d798c0` the filter over the solution (246, 38, 72,
      338 green, 43 s, 122 samples of `nvidia-smi --query-compute-apps`) brought up no
      compute process; with the red applied, `testhost.exe` of GPU.Test appeared. D1's
      no-GPU cases run on injected absence (`DeviceSelector.Open(…, isPresent)`,
      `GpuBuilder.WithDevicePresence`), the device branches under **Gpu** (61 of 61 on the
      device). OpenCL is held by the code only (no device-side measure).
- [ ] **A13, the stop word without a synchronisation** (PERF-5). With a stagnation limit,
      the control block is copied to page-locked host memory every 16 generations without
      synchronising the accelerator, and the copy is read at the next interval; only the
      observer and the end synchronise: `PopulationTransfers` counts observer calls + 1
      synchronising reads in a run. S12, S17 and S18 stay green. Red: the synchronous read
      restored (⌈G/16⌉ more).
