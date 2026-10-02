# BOOT.md — executor contract

## Purpose

The seam between the threading (`Controllers`) and the algorithm (`AlgorithmExecutors`):
a worker knows only that it calls `Execute` with its id once per generation.

## Invariants

- **The executor learns nothing about threads but its worker id.** Held by the
  signature.

## Dependencies

None.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `Execute` is called concurrently by every worker; an implementation must touch only
  its own stripe.

## Acceptance criteria

- [x] Driven through this contract by the worker tests: 2026-10-02,
      `WorkerControllerTests`, `WorkersOrchestratorTests` (integration, full local run).
- [ ] ⚠ One implementation; the contract exists for the worker tests' fakes.

## Taboos

- **No shared mutable state between concurrent `Execute` calls** other than disjoint
  indices of the next population and the trial records.
