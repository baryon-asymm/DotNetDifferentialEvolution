# BOOT.md — GPU random generator contract

## Purpose

The kernel-side random-number contract: a draw is addressed by an index, so that each
GPU thread owns a stream and no two threads share mutable state. Kept apart from its one
implementation so that the mutation strategy and the controller depend on the
contract and a caller can supply another generator struct.

## Invariants

- **A draw is addressed by an index, and the package always passes the individual's
  index.** That is what keeps streams per thread and avoids races. Held by the shape of
  the code of the mutation strategy
  ([MutationStrategies](../../MutationStrategies/API.md)), the only caller.

## Dependencies

None.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Implementations are kernel code: structs that ILGPU can compile.

## Acceptance criteria

- [x] The one implementation is used through this interface inside the kernels:
      2026-10-02, the end-to-end tests in `DifferentialEvolutionOptimizerTests`
      (local, OpenCL `gfx1036`).
- [ ] ⚠ The contract states no ranges (`Next` non-negative? `NextDouble` in [0, 1)?)
      although the mutation strategy relies on them: `Next(index) % n` assumes a
      non-negative value. An implementation returning negatives would index outside the
      population.
- [ ] ⚠ `NextFloat` and `NextUInt` are part of the contract and used by nothing in the
      package.

## Taboos

- **No member that draws without an index.** A shared stream across GPU threads is a
  data race on device memory.
