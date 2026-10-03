# BOOT.md — GPU mutation contract

## Purpose

How a trial vector is built, as kernel code generic over the random generator. Kept
apart from its one implementation so that the controller is generic over the scheme and
a caller can supply another mutation struct.

## Invariants

- **A mutation reads the whole current population but writes only trial `index`.**
  Reading others is safe: the current population is not written during the run kernel
  (selection writes the next one). Held by the shape of the run kernel
  ([Kernels](../../Controllers/Kernels/API.md)).
- **The generator type is a generic parameter, not an interface value.** That is what
  lets ILGPU compile the call without virtual dispatch.

## Dependencies

- [Models](../../Models/API.md) — `DevicePopulation`.
- [RandomGenerators/Interfaces](../../RandomGenerators/Interfaces/API.md) —
  `IRandomGenerator`, the constraint on the generic parameter.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Implementations are kernel structs that ILGPU can compile.

## Acceptance criteria

- [x] Used by the run kernel through this contract, and the run converges: 2026-10-02,
      the end-to-end tests in `DifferentialEvolutionOptimizerTests` (local, OpenCL
      `gfx1036`).
- [ ] ⚠ The contract carries no control parameters per individual (F, CR): an adaptive
      scheme such as jDE cannot be expressed through it without its own device state.

## Taboos

- **No write into `currentPopulation`.** Other threads of the same launch read it as
  their donors.
- **No interface-typed generator parameter.** It would need virtual dispatch, which a
  kernel cannot compile.
