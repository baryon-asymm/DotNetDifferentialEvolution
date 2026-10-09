# ACCEPTANCE.md — DotNetDifferentialEvolution.GPU/Kernels

The node's acceptance criteria (AGENTS.md 3.2, §6). Its checks from 2026-10-03 and
2026-10-05 — 1b–1g, 2b, 8a–8c, S2–S6 and S16 — stay in the package's
[ACCEPTANCE.md](../ACCEPTANCE.md), whose file is at its limit (§15). The pointwise
checks P0–P5 are here, under that file's rules (proven twice: green on a known answer and
red once on the named mutation, applied to a scratch copy and never committed; frozen; CI
unless marked **Gpu**). P2 holds the package root's builder, whose file is full.

## The pointwise objective — checks P0–P5, frozen 2026-10-09, before code

- [ ] **P0, the single-kernel path unchanged.** At the base commit, before any code
      changes, a test runs each of the nine configurations of check S13 (Sphere D = 10 in
      [−5, 5], N = 100, L-SHADE N_init 180, seed 1, 2·10⁴ evaluations) on the CPU
      accelerator and takes one SHA-256 over, configuration by configuration in S13's
      order: the best genes and the fitness as IEEE-754 bits, the generation and the
      evaluation counts. The hash measured at the base is written into the test and
      committed before the refactoring (green by construction); the test stays green after
      it. Red: jDE's F and CR handed to the individual when its parent is kept.
- [ ] **P1, the same run as a monolithic objective.** A pointwise objective and its
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
      trial not handing jDE its F and CR in the pointwise path; the build and select
      kernels ignoring the stop word, seen in a population snapshot taken after the
      stopping generation and before the word is read. ⚠ Changed 2026-10-09 after code:
      the frozen red was the select kernel alone, which cannot turn red — with no new
      trial and no new results it re-selects a survivor against the same trial, a fixed
      point (measured: 11 of 11 green); build and select together put new trials beside
      stale results, which only a snapshot shows (without one, also 11 of 11 green).
- [ ] **P2, the builder.** `ForPointwiseFunction(f, 0)` and `(f, −1)` throw
      `ArgumentOutOfRangeException`; `WithPopulationSize` refuses `N·P > int.MaxValue`
      (N = 2²⁰, P = 2¹²) with the exception it throws for `N·D`; the existing
      `ForFunction` tests compile and pass unchanged. Red: `P = 0` accepted.
- [ ] **P3, data and math on CUDA** (**Gpu**). A pointwise objective that carries an
      `ArrayView<double>` field and calls `Exp` and `Pow` in `EvaluatePoint`, with a
      result struct of two `double`s and an `int`, builds and runs on CUDA and equals its
      monolithic twin bit for bit (L-SHADE, N_init 1 024, P = 50, seed 1, 50 generations).
      Red: the pointwise kernels loaded without `KernelLoader` (no libdevice post-link):
      `Build` throws.
- [ ] **P4, the point of it: latency** (**Gpu**). An objective of P = 50 points, each 40
      rounds of `Exp` and `Pow` on doubles, under DE/rand/1/bin on CUDA: per generation,
      the median of three timed batches of 50 generations after one warm-up batch, at
      N = 1 024 and N = 16 384, pointwise against its monolithic twin. Pass: at
      N = 1 024 the pointwise generation is at least 4× faster; both figures at both N
      are recorded. Red: the point kernel launched with N threads, each looping over its
      `P` points.
- [ ] **P5, the kernel guards cover the new kernels.** Checks 8a–8c find kernel entry
      points by their first parameter (`Index1D`), so the pointwise kernels are guarded
      without registration. Red: a `throw` in `EvaluatePoints` turns 8a red.
