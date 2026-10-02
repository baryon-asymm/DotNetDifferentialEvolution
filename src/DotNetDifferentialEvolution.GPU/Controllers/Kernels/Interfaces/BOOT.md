# BOOT.md — GPU kernel controller contract

## Purpose

The seam between the optimizer and the generic controller. The controller has four
struct type parameters; the optimizer holds it through this non-generic interface, so
it does not carry them.

## Invariants

- **The contract is non-generic.** That is its reason to exist: the optimizer and a
  caller holding a controller need not name the strategy types. Held by the shape of
  the code.

## Dependencies

- [Models](../../../Models/API.md) — `HostPopulation`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Host code.

## Acceptance criteria

- [x] The optimizer drives a run through this contract: 2026-10-02, the end-to-end
      tests in `DifferentialEvolutionOptimizerTests` (local, OpenCL `gfx1036`).
- [ ] ⚠ The lifecycle order (compile, init, run) lives only in XML comments; nothing
      in the contract makes `Run` before `Init` impossible.
- [ ] ⚠ `Dispose` is part of the contract, but what it disposes is not stated here;
      the implementation also disposes the caller's `Context` and `Accelerator`.

## Taboos

- **No generic parameters on this interface.** It is the one place the strategy types
  are erased; adding them back makes every holder generic again.
