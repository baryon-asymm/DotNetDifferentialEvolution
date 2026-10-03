# BOOT.md — GPU MutationStrategies

## Purpose

The default trial generator of the GPU package: classic DE/rand/1/bin with constant
mutation force F (default 0.3) and crossover rate CR (default 0.8), executed by the
thread that owns the trial. Out-of-box genes are re-drawn uniformly in the box.

## Invariants

- **The three donors are distinct from each other and from the target.** Redraw on
  collision, shift past the target index. Held by the code; corrected once (see
  Taboos); no unit test.
- **Every gene of trial `index` is written.** Both crossover branches write it. Held by
  the shape of the code.
- **Every gene of the trial lies in `[lower, upper]`** when it comes from the mutant
  (checked, else redrawn in the box) and is the parent's otherwise; so the trial stays
  in the box as long as the parent is. Held by the code.

## Dependencies

- [Models](../Models/API.md) — `DevicePopulation`.
- [RandomGenerators/Interfaces](../RandomGenerators/Interfaces/API.md) —
  `IRandomGenerator`, the generic constraint and every draw.

Outside the tree: ILGPU 1.5.1 (`ArrayView<double>` for the bounds).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Kernel code: a `readonly struct`. The donor indices live in a 3-element local array
  (`new int[3]`), which ILGPU accepts: the end-to-end tests pass with it on OpenCL.

## Acceptance criteria

- [x] With this scheme the run converges on Rosenbrock (2-D) and on a 6-coefficient
      polynomial fit: 2026-10-02, `DifferentialEvolutionOptimizerTests` (local, OpenCL
      `gfx1036`), population 10 000, 1 000 generations, F 0.3, CR 0.8.
- [ ] No unit test of the donor rule, the crossover or the bound repair.
- [ ] ⚠ No `jrand`: a trial can equal its parent, wasting an evaluation. The CPU
      package's strategies all guarantee one mutant gene.
- [ ] ⚠ Population size is not checked: `N <= 3` hangs the kernel or divides by zero.
- [ ] ⚠ The bound parameters come lower-first here and upper-first in
      `PopulationSamplingMaker`; both take the same kind of argument, so a swap compiles.
- [ ] ⚠ An out-of-box mutant gene is re-drawn uniformly; the CPU package's adaptive
      variants reflect halfway toward the parent instead.

## Taboos

- **No collision fix by incrementing the drawn index.** That was the rule before
  `1ad5a86` (2024-08-10, "Corrected random Individual selection"): bump the index and
  rescan, which biases the choice and could land on the target. A collision is redrawn.
- **No writes outside trial `index`.** Other threads use the same population as donors.
