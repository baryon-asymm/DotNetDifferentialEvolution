# BOOT.md — DotNetDifferentialEvolution.GPU/Bookkeeping

## Purpose

Designed and built 2026-10-05 (the package's
[HISTORY.md](../HISTORY.md#symmetry-decided-2026-10-05)).

The work between two generations, on the device, for the configurations that need it:
the best index (best/1, best/2, current-to-best/1, the stagnation rule), the fitness
ranking (JADE, SHADE, L-SHADE), the archive, JADE's means and SHADE's memory,
L-SHADE's population reduction, and the stagnation rule. It is the GPU's counterpart of
the CPU package's `OrchestratorWorkerHandler` and its generation strategies; the
generation itself stays in [Kernels](../Kernels/API.md).

## Invariants

- **The CPU package's order of operations** after a generation: the ranking of the new
  population, then the archive, then the adaptation, then L-SHADE's reduction, then the
  best index, then the stop rule. Before the first generation: the ranking and the best
  index of the initial population, as the CPU builder computes them.
- **One total order.** Ranking and best index order by (key, index) with `NaN` as +∞:
  ties go to the lower index. Ranking compares integer order keys (`FitnessOrder.OrderKey`,
  the same order, ±0 equal, NaN as +∞; A3). Ranking by counting (N ≤ the limit) and the
  bitonic network (above) give the same order (check S9), so the limit changes speed, never
  a result. The limit is the instance's, timed on CUDA and OpenCL in the constructor
  (A3 ⚠; HISTORY.md#ranking-calibrated-2026-10-10, decision 2).
- **Passes that cannot depend on their order are wide** (A4): the best index, the improved
  count, the archive placement and the largest weight run over wide chunks; the sums
  (`SumSuccesses`) keep chunks of 1 024, the order S7 holds. The wide chunk is
  `max(32, 32·⌈⌈√N_init⌉/32⌉)`, fixed for the run (A4 ⚠).
- **Deterministic sums.** Every sum runs in index order within chunks of 1 024 and over
  the chunks in chunk order; no floating-point atomic anywhere. For N ≤ 1 024 that is
  the CPU package's order bit for bit (S7).
- **SHADE and L-SHADE use one weight scale across all chunks** (S19, F7).
  `LargestWeights` takes the maximum difference from improved trials with finite endpoints
  in each wide chunk. `SumSuccesses` takes the maximum across chunks before forming any products.
  `WeightOf` marks an excluded record with NaN. Reject that marker before comparing maxima.
  ⚠ 2026-11-13: the previous maximum relied on `weight > maximum` alone. On gfx1036 the
  generated OpenCL selected the candidate when `weight <= maximum` was false, including NaN.
  This corrupted the raw sums even in generations whose final memory the extended pass recovered.
  For a finite maximum above `double.MaxValue / (2N)`, divide weights by that maximum.
  An overflowing difference selects `(parent/2 − trial/2) / (double.MaxValue/2)` for every weight.
  Below 2⁻⁹⁷⁰, subtract the endpoints in units of 2⁻¹⁰²², decoding subnormal significands
  from their bits. This also handles a device reporting a zero maximum for subnormal differences.
  Otherwise use the original weights. JADE uses unit weights for improved trials.
  ⚠ 2026-11-13 (F7): the previous rule skipped overflowing differences and applied no scale at
  the low end. The extreme-weight memory checks in `ExtremeImprovementTests` exposed both cases.
- **Check the ordinary moment sums before writing each ratio.** For numerator A, denominator B
  and N active trials, A ≤ max(N, B)·2⁻⁹⁶⁹ or B ≤ Σw·2⁻⁹⁶⁹ selects extended-exponent
  recomputation. The second bound covers a subnormal parameter whose product with a large
  weight contributes materially to the Lehmer denominator.
  The existing final adaptation thread rereads the active records only on this route (O(N)).
  It accumulates all five nonnegative moments in each chunk and aligns each exponent again
  when adding that chunk to the total. Only the affected ratio uses these sums. There is no
  extra kernel launch or host transfer. This path retains binary64 significand precision.
  A subnormal quotient is packed from its bits, rounded to nearest even.
  The maximum CR uses ordered binary64 bits from +0, including across chunks. An all-zero
  CR set skips CR recomputation. L-SHADE stores the terminal sentinel for that set.
- **The archive is the CPU package's loop, parallel** (S8): an improved parent's fill
  position is the archive size before the generation plus the number of improved parents
  before it; below the capacity it is its slot, at or above it the parent draws a
  uniform slot from its own stream 1; of two parents on one slot the higher index stays
  (an integer `Atomic.Max`, independent of the order the threads run in).
- **Pure functions under every kernel.** Each rule (the order, the adaptation updates,
  the slot draw, L-SHADE's schedule, the stop rule) is a static function the kernels
  call and the tests call on the host, against the CPU package's classes.
- **Stagnation compares the original scores** (S12). NaN in either score or equal
  infinities count one generation toward the streak and preserve the last reference score.
  Distinct ordered scores involving infinity have infinite distance. For finite scores
  use binary64 absolute subtraction, including overflow, and reset only above the threshold.
  ⚠ 2026-11-13: the former unguarded comparison kept resetting the streak for a constant
  infinity or NaN on physical OpenCL. The public child-process test reproduced all three cases.
- **After the stop rule fires, nothing writes.** Every kernel of a later generation,
  the generation kernel included, returns at once; the host restores its own counters to
  the generation that stopped (check S12).
- **Kernel code as the package's** (checks 8a–8c, S16).

## Dependencies

- [Kernels](../Kernels/API.md) — `PopulationViews`, `StepParameters`, the strategy views.
- [Random](../Random/API.md) — `PhiloxDraws` on stream 1.
- [Devices](../Devices/API.md) — `KernelLoader`, through which every kernel is loaded.

Outside the tree: ILGPU 1.5.3 (`Index1D`, `ArrayView<T>`, `Atomic.Max`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Implicitly grouped kernels only, loaded through `Devices.KernelLoader`; no shared
  memory and no group barrier, so the CUDA post-link path is the one already proven.
- A configuration loads only the kernels it uses: rand/1 with a limit loads none.
- L-SHADE's population sizes and archive capacities are computed on the host from the
  evaluation count; buffers are allocated once for the initial N.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- **No floating-point atomic and no reduction whose order depends on scheduling.**
- **No host read inside a generation** other than the stop rule's control block.
- **No semantics of its own**: a rule that differs from the CPU package's is a defect
  here or a ⚠ decision in the package's `HISTORY.md`, never a silent choice.
