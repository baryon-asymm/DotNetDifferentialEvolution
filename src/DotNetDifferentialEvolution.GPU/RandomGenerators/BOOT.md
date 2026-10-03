# BOOT.md — GPU RandomGenerators

## Purpose

The package's one random generator: per-thread `XorShift32` states living in device
memory, so that a kernel draws random numbers without any host round trip. It replaced
a host-side generator on 2024-08-10 (see Taboos), and exists as its own node because the
mutation strategy is generic over the generator.

## Invariants

- **The state of stream `i` lives in `xorShifts[i]` on the device and is written back
  after every draw.** Without the write-back every draw of a generation would repeat
  the same value. Held by the shape of the code.
- **Every draw consumes two steps of the stream** (one value returned, one discarded
  by `NextProvider()`). Measured 2026-10-02 on the host, seed 42: the pattern yields
  values 1, 3, 5, 7, 9 of the raw sequence. Harmless for quality; matters for anyone
  reproducing a run step by step.

## Dependencies

None.

Outside the tree: ILGPU 1.5.1, ILGPU.Algorithms 1.5.1 (`XorShift32`, `ArrayView`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Kernel code: the struct holds only an `ArrayView` and must compile under ILGPU.
- CA1815 is off for this package (`.editorconfig`): the struct is never compared.

## Acceptance criteria

- [x] Draws inside the kernels give a search that converges: 2026-10-02, the
      end-to-end tests in `DifferentialEvolutionOptimizerTests` (local, OpenCL
      `gfx1036`).
- [x] `Next` is non-negative and `NextDouble` lies in (0, 1) for `XorShift32`:
      2026-10-02, host-side measurement in the session scratchpad, 2 000 000 draws each,
      ILGPU.Algorithms 1.5.1. Not a test in the repository.
- [ ] No test in the repository checks ranges, stream independence or the write-back.
- [ ] ⚠ The buffer length is not checked against the population size; a short buffer
      makes kernel threads read and write outside it.
- [ ] ⚠ A zero seed terminates the process (ILGPU's assertion), and seeding is left to
      every caller: the tests seed with `(uint)Random.Shared.Next()`, which can be 0.
- [ ] ⚠ No way to seed a run reproducibly through the package.

## Taboos

- **No random numbers generated on the host and copied to the device per
  generation.** That was the previous design (`DeviceRandomController`, pages of host
  `Random` values copied each generation), removed in `1ad5a86` (2024-08-10) for GPU
  efficiency.
- **No draw without writing the state back.** The stream would stop advancing and
  every draw in a generation would return the same value.
