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
  a result. ⏳ 2026-10-10 (A3 ⚠): the limit is the instance's, timed on the device in the
  constructor (HISTORY.md#ranking-calibrated-2026-10-10, decision 2); built: 2 048 fixed.
- **Passes that cannot depend on their order are wide** (A4): the best index, the improved
  count, the archive placement and the largest weight run over wide chunks; the sums
  (`SumSuccesses`) keep chunks of 1 024, the order S7 holds. ⏳ 2026-10-10 (A4 ⚠): the wide
  chunk is `max(32, 32·⌈⌈√N_init⌉/32⌉)`, fixed for the run; built: 32.
- **Deterministic sums.** Every sum runs in index order within chunks of 1 024 and over
  the chunks in chunk order; no floating-point atomic anywhere. For N ≤ 1 024 that is
  the CPU package's order bit for bit (S7).
- **SHADE's weights are scaled before they can overflow a sum** (S19). A pass finds each
  chunk's largest weight; when the generation's largest exceeds `double.MaxValue / (2N)`
  every weight is divided by it, else the weights are used as they are (division by 1.0
  changes no bit). Two finite improvements near `double.MaxValue` otherwise sum to `+∞`,
  and `∞/∞` writes `NaN` into the memory. The CPU package's `ShadeStrategy` applies the
  same rule. The parts: `AdaptationRules.ScaleOf(double largestWeight, int count)` returns
  the divisor (`largestWeight > double.MaxValue / (2.0 * count) ? largestWeight : 1.0`); the
  kernel `LargestWeights` (one thread per chunk, the chunk's largest SHADE weight from
  `WeightOf`, 0 when none) runs under SHADE and L-SHADE only, before `SumSuccesses`;
  `SumSuccesses` takes the chunks' largest weights, takes their maximum, and adds
  `weight / scale` (JADE: scale 1). The sums keep their index order.
- **The archive is the CPU package's loop, parallel** (S8): an improved parent's fill
  position is the archive size before the generation plus the number of improved parents
  before it; below the capacity it is its slot, at or above it the parent draws a
  uniform slot from its own stream 1; of two parents on one slot the higher index stays
  (an integer `Atomic.Max`, independent of the order the threads run in).
- **Pure functions under every kernel.** Each rule (the order, the adaptation updates,
  the slot draw, L-SHADE's schedule, the stop rule) is a static function the kernels
  call and the tests call on the host, against the CPU package's classes.
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
