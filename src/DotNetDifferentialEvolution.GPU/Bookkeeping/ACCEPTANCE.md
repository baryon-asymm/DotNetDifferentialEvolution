# ACCEPTANCE.md — DotNetDifferentialEvolution.GPU/Bookkeeping

The node's acceptance criteria (AGENTS.md 3.2, §6): checks S7–S12 of the symmetry with the
CPU package. Written 2026-10-05 in the package's [ACCEPTANCE.md](../ACCEPTANCE.md), before
the code, under the rules and the meaning of "parity" stated there (proven twice, frozen,
CI unless marked **Gpu**); moved here unchanged on 2026-10-05, when the package's file
reached its limit (§15). S6 and S14, which also exercise this node, stay in the package's
file.

## Symmetry with the CPU package — checks S7–S12, frozen 2026-10-05, before code ✅

- [x] **S7, adaptation, parity.** From 200 random sets of trial records (N in [4, 1 024],
      improved, accepted and kept; infinite and `NaN` fitness among them), JADE's μCR and
      μF and the SHADE and L-SHADE memories equal the CPU strategies' after
      `AfterGeneration` with the same records, bit for bit; the CPU state is read through
      `GetControlParameters` with draws that return the means exactly. The device kernels
      equal the host functions bit for bit, also for N up to 5 000 (several chunks). Red:
      SHADE's CR mean taken as Lehmer; the memory index advanced with no success.
      2026-10-05: `GPU.Test/Bookkeeping/AdaptationParityTests` (seed 20261008; the device
      cases over three generations, the first without success, after a green red run →
      [HISTORY.md](../HISTORY.md#symmetry-built-2026-10-05)); red with SHADE's CR mean as
      Lehmer (set 1, SHADE, N 973, slot 0) and with the index advanced without success
      (L-SHADE, N 4, entry 0).
- [x] **S8, archive.** The host functions equal `AdaptiveStrategyBase.UpdateArchive`
      (through `JadeStrategy.AfterGeneration` with a recording provider) given the same
      slot draws in index order: filling, filling into overflow, full with collisions,
      capacity 0. The device kernels equal the host functions bit for bit, archive and
      size. Red: a collision kept for the earlier index; the fill position ignoring the
      size before.
      2026-10-05: `GPU.Test/Bookkeeping/ArchiveParityTests` (seed 20261009, 200 cases); red
      with the earlier index kept (`CompareExchange` for `Atomic.Max`: N 3 000, capacity 7)
      and with the size before taken as 0 (N 100, capacity 30, size 12).
- [x] **S9, ranking** (device). For 200 random arrays (N in [1, 20 000], with `NaN`, ±∞,
      ±0 and ties), ranking by counting and the bitonic sort each give exactly the order
      by (key, index), `NaN` as +∞; where the keys are distinct, the CPU
      `PopulationSortHelper`'s order. Red: `NaN` not mapped; ties by the higher index.
      2026-10-05: `GPU.Test/Bookkeeping/RankingTests` (seed 20261010; 6 edge sizes, 150 by
      counting, 44 bitonic only); red with `NaN` not mapped and with ties by the higher index.
- [x] **S10, best index** (device). The best-index kernels equal `BestPick.IndexOf` on
      200 random arrays (N in [1, 5 000], ties across the 1 024-chunk boundary, all
      `NaN`). Red: `<=` for `<`.
      2026-10-05: the same class, seed 20261011; red with `IsBetterOrEqual` for `IsBetter` in
      the chunk pass (array 1, N 1 869: 46 expected, 1 023 found).
- [x] **S11, L-SHADE's schedule.** N per generation equals `LShadeStrategy`'s formula:
      for N_init 100 and a budget of 10 000, the known answer (computed 2026-10-05 with
      exact decimal rounding, outside the package) is 98, 97, 96, 95, 94, 93, 93, 92, 91,
      90, 89, 88 for the first twelve generations, 333 generations in all, N = 4 at the
      end and 10 000 evaluations. A device run's observer sees that N each generation and the
      first N of the ranking, in order; the archive capacity is `round(2.6·N)` and its
      size is truncated. `Build` refuses an evaluation limit other than the budget with
      `InvalidOperationException`. Red: midpoint to even; survivors from the unranked
      population.
      2026-10-05: `GPU.Test/Bookkeeping/LShadeReductionTests`; red with `ToEven` (the known
      answers) and with survivors taken unranked (not in ranking order; the CPU and GPU
      runs part).
- [x] **S12, stagnation.** The device rule equals `StagnationStreakTerminationStrategy`
      on 100 scripted sequences of best values (`NaN`, ±∞, threshold 0, a first value
      equal to `double.MinValue`). A run whose objective stops improving ends at the
      generation that rule gives for its observed best values, with the same generations,
      evaluations and result for a read every generation and every 16. Red: the streak
      reset on equality.
      2026-10-05: `GPU.Test/Bookkeeping/StagnationTests` (seed 20261012); red with the
      streak reset on equality (sequence 0, step 0; and the run, which now ends at
      generation 10 000 by its observer: the first red run hung →
      [HISTORY.md](../HISTORY.md#symmetry-built-2026-10-05)).

## Overflow of SHADE's weights — check S19, frozen 2026-10-08, before code ✅

Found 2026-10-07 in PastyPropellant: the CPU `ShadeStrategy` and these rules let two finite
improvements near `double.MaxValue` sum to `+∞`, and `∞/∞` wrote `NaN` into the memory (CPU
criteria O1–O3, [Shade/BOOT.md](../../DotNetDifferentialEvolution/Algorithms/Shade/BOOT.md)). The
rule fixed there: when the generation's largest weight exceeds `double.MaxValue / (2·N)`, every
weight is divided by it; otherwise the weights are used as they are.

- [x] **S19, overflow, parity.** From 200 random sets of trial records with parents scored
      `double.MaxValue` (N in [4, 1 024]; improvements from tiny to `double.MaxValue`; `NaN` and
      infinite parents; JADE unaffected), the SHADE and L-SHADE memories equal the CPU
      strategies' after `AfterGeneration` with the same records, bit for bit, all finite;
      the device kernels equal the host functions bit for bit for N up to 5 000, where the
      largest weight is in another chunk than the trial it scales, over three generations of
      which the first has no success. A run of the public builder on the ILGPU CPU
      accelerator (SHADE and L-SHADE; an 8-D sphere feasible on `x₀ < −4` and
      `double.MaxValue` elsewhere, the objective scoring a `NaN` gene as 0) ends with no `NaN` gene
      in its best individual. Red: the scale never applied (the weights as they are).
      Where: `GPU.Test/Bookkeeping/AdaptationParityTests` (the existing CPU-parity and
      device cases, extended with parents at `double.MaxValue`: one in four for the CPU parity,
      one in 700 for the device at N 1 025 to 5 000, so most chunks hold none; the host
      composition there applies `ScaleOf` from the generation's largest weight) and
      `GPU.Test/EndToEnd/SentinelFitnessTests` (population 100, 20 000 evaluations, seed
      12345). A `Gpu`-category case of the same run on CUDA (L-SHADE, population 16 384, 32
      genes, 5 000 000 evaluations, seed 20261007) is the orchestrator's to run.
      2026-10-08: `AdaptationParityTests` (seed 20261008 + 19; the CPU parity 200 sets, one
      parent in four at `double.MaxValue`, JADE sets included; the device theory JADE 5 000,
      SHADE 1 025 and 3 000, L-SHADE 4 and 5 000, one parent in 700 above N 1 024 and one in
      two below) and `SentinelFitnessTests` (CPU accelerator: SHADE 199 and L-SHADE 669
      generations; CUDA, RTX 5070 Ti, `Category=Gpu` 44/44 with it); written by a coder,
      rerun by the orchestrator. On the rule unfixed: the CPU parity red (set 1, SHADE N 872:
      NaN against 0.5153738326306604), the device cases red, both CPU-accelerator runs ending
      with a `NaN` gene and fitness 0. Red with the scale never applied (`ScaleOf` always 1):
      7 tests, and the CUDA case; with each chunk's scale from its own largest weight: the
      device cases at N 3 000 and 5 000; with the scale at every bound: both CPU-parity tests.
      The non-GPU suite 319/319 →
      [HISTORY.md](../HISTORY.md#shade-weight-overflow-2026-10-08).

## Audit fixes — checks A3–A4, frozen 2026-10-10, before code

From the audits of 2026-10-09 ([HISTORY.md](../HISTORY.md#audit-fixes-decided-2026-10-10)),
under the rules above.

- [ ] **A3, ranking by integer keys, and its limit** (PERF-7, PERF-3).
      `FitnessOrder.OrderKey` orders −∞, −`double.MaxValue`, −1, −ε (the smallest
      subnormal), −0, +0, ε, 1, `double.MaxValue`, +∞, NaN non-decreasingly, with −0 = +0 and
      +∞ = NaN; ranking compares (key, index) as integers; S9 and P0's hash stay green.
      Counting runs up to N = 2 048, the bitonic network above. **Gpu**, CUDA:
      `RankByCounting` at N = 2 048 is not slower than `RankByBitonicNetwork` at 2 048
      (measured 2026-10-09 with integer keys: 263 against 421 µs). Red: −0 and +0 given
      different keys (CI); the comparison on doubles again (**Gpu**; measured 686 against
      522 µs).
- [ ] **A4, order-independent passes in chunks of 32** (PERF-8). The best index, the
      improved count, the archive placement and the largest weight run over chunks of 32
      (more partials, the same combine); the best index carries its incumbent's value in a
      register; `SumSuccesses` skips the division when the scale is 1.0 and keeps chunks of
      1 024. S7, S8, S10, S19 and P0's hash stay green. **Gpu**, CUDA: `FindBest` at
      N = 1 024 takes at most 25 µs per call (about 105 µs at `c40868e`, measured
      2026-10-09). Red: chunks of 1 024 again for these passes (**Gpu**).
