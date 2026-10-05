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
