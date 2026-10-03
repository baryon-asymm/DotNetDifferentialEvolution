# BOOT.md — Interfaces

## Purpose

The package-level hooks that are neither strategies nor variants: how the initial
population is made, and who is told about each generation. They sit at the package root
level because both the builder and the engine refer to them.

## Invariants

- **A seeded run reaches the sampler only through `UseRandomProvider`.** The default
  ignores it, so third-party samplers written before seeding existed keep working
  (`3f3d394`). Held by the default interface member.
- **The observer gets the live engine object.** No copy is made per generation. Held by
  the signature and the engine.

## Dependencies

- [Models](../Models/API.md) — `Population`, the observer's argument.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Default interface members are part of the contract here; removing one breaks every
  implementation that relied on it.

## Acceptance criteria

- [x] The built-in sampler honours the contract: 2026-10-02,
      `UniformRandomSamplingMakerTests` (local run, part of 115 of 115 unit cases for
      slice 4).
- [ ] No test of `IPopulationUpdatedHandler` beyond its use in integration tests.
- [ ] ⚠ Part of the `Models` cycle: `IPopulationUpdatedHandler` takes a `Population`,
      and `ProblemContext` holds an `IPopulationUpdatedHandler`.
- [ ] ⚠ The directory name `Interfaces` says what the files are, not what they are for;
      the two hooks have nothing in common but being interfaces.

## Taboos

- **No abstract `UseRandomProvider`.** It was added as a default member so existing
  samplers stay valid (`3f3d394`); making it abstract breaks them for no gain.
