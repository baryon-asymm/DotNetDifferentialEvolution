# BOOT.md — DotNetDifferentialEvolution.GPU/Kernels

## Purpose

The device side of a run: the initial-sampling kernel, the generation kernel, and the
DE/rand/1/bin step they share, written as static functions over a draw source so the
step can be tested on the host with scripted draws. One thread per individual.

Designed and built 2026-10-05 (the package's
[HISTORY.md](../HISTORY.md#symmetry-decided-2026-10-05)): the generation kernel builds the
trial by any of the CPU package's schemes, draws F and CR by its parameter rules (fixed,
jDE, JADE, SHADE), and selects with or without ties, recording what the
[Bookkeeping](../Bookkeeping/API.md) passes need.

## Invariants

- **The step is the CPU package's.** Donors, mutant, binomial crossover with `jrand`,
  midpoint repair and survival follow `docs/ALGORITHMS.md` §§2–3 and §9, and consume
  draws in the same order as the CPU `MutationStrategy`, so the same draws give the same
  trial bit for bit. Held by ACCEPTANCE.md, checks 1b–1e and 1g.
- **Thread i writes only trial slot i and next slot i.** Every thread reads the whole
  current population; nothing else is written. A launch therefore needs no
  synchronisation. Held by check 2b.
- **Survival is `f(u) ≤ f(x)`, `NaN` worst, two `NaN`s no tie.** Held by check 1e.
  Under jDE and JADE ties are refused: survival is `f(u) < f(x)` (S5).
- **The draw order of the CPU executor**: F and CR first (its control-parameter
  provider), then the scheme's indices, then `jrand` and the crossover draws. Each
  scheme and rule is the CPU class's, draw for draw (S2–S4).
- **Thread i also writes only entry i** of jDE's F and CR and of the trial records.
- **Kernel code compiles on every backend**: nothing a kernel reaches throws, allocates
  or boxes; `Math` only from the allow-list. Held by checks 8a–8c.
- **Populations are individual-major**: individual i is genes `[i·D, (i+1)·D)`.

## Dependencies

- [Objectives](../Objectives/API.md) — `IGpuFitnessFunction`, `GeneView`.
- [Random](../Random/API.md) — `PhiloxDraws`, `IDrawSource`.

Outside the tree: ILGPU 1.5.3 (`Index1D`, `ArrayView<T>`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Indices are `int`: the builder refuses `N·D > int.MaxValue`.
- No constant on the left of an ordered floating-point comparison: ILGPU moves it to the
  right and inverts its `NaN` ordering (APT root `BOOT.md`, the third ILGPU defect).
- A kernel entry point is any method whose first parameter is an `Index1D`; the guards
  find them that way, so a new kernel is guarded without registration.

## Acceptance criteria

→ checks 1b–1g, 2b, 8a–8c and S2–S6, S16 of the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Taboos

- **No write outside slot i of the trial and next buffers.**
- **No draw consumed in a different order from the CPU step.** Parity (check 1g) is what
  lets the CPU package's semantics be argued for this one.
- **No `throw`, no allocation, no virtual call in kernel code.**
