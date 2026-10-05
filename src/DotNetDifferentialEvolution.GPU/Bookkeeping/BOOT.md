# BOOT.md — DotNetDifferentialEvolution.GPU/Bookkeeping

## Purpose

⏳ Designed 2026-10-05 (the package's
[HISTORY.md](../HISTORY.md#symmetry-decided-2026-10-05)); no code yet.

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
  ties go to the lower index. Ranking by counting (N ≤ 8 192) and the bitonic network
  (above) give the same order (check S9).
- **Deterministic sums.** Every sum runs in index order within chunks of 1 024 and over
  the chunks in chunk order; no floating-point atomic anywhere. For N ≤ 1 024 that is
  the CPU package's order bit for bit (S7).
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

→ checks S6–S12 and S14 of the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Taboos

- **No floating-point atomic and no reduction whose order depends on scheduling.**
- **No host read inside a generation** other than the stop rule's control block.
- **No semantics of its own**: a rule that differs from the CPU package's is a defect
  here or a ⚠ decision in the package's `HISTORY.md`, never a silent choice.
