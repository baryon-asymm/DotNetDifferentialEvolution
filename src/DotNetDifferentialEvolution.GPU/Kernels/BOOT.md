# BOOT.md — DotNetDifferentialEvolution.GPU/Kernels

## Purpose

The device side of a run: the initial-sampling kernel, the generation kernel, and the
DE/rand/1/bin step they share, written as static functions over a draw source so the
step can be tested on the host with scripted draws. One thread per individual.

## Invariants

- **The step is the CPU package's.** Donors, mutant, binomial crossover with `jrand`,
  midpoint repair and survival follow `docs/ALGORITHMS.md` §§2–3 and §9, and consume
  draws in the same order as the CPU `MutationStrategy`, so the same draws give the same
  trial bit for bit. Held by ACCEPTANCE.md, checks 1b–1e and 1g.
- **Thread i writes only trial slot i and next slot i.** Every thread reads the whole
  current population; nothing else is written. A launch therefore needs no
  synchronisation. Held by check 2b.
- **Survival is `f(u) ≤ f(x)`, `NaN` worst, two `NaN`s no tie.** Held by check 1e.
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

→ checks 1b–1g, 2b and 8a–8c of the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Taboos

- **No write outside slot i of the trial and next buffers.**
- **No draw consumed in a different order from the CPU step.** Parity (check 1g) is what
  lets the CPU package's semantics be argued for this one.
- **No `throw`, no allocation, no virtual call in kernel code.**
