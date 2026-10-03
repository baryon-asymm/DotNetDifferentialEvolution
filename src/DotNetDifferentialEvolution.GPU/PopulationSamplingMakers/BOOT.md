# BOOT.md — GPU PopulationSamplingMakers

## Purpose

The default initial population: points drawn uniformly in the search box on the host.
It also carries the population size, which the whole run uses as the kernel extent.

## Invariants

- **Every gene of every sample lies in `[lower[j], upper[j])` when `lower[j] <=
  upper[j]`.** Held by the formula in `TakeSamples` and `NextDouble`'s range; no test.
- **The constructor rejects a non-positive size and bounds of different lengths.**
  Held by the code; no test.
- **Bounds are copied at construction** (`ToArray().AsReadOnly()`), so a later change
  of the caller's arrays does not move the box.

## Dependencies

None.

Outside the tree: .NET 8 (`Random.Shared`, `Parallel.For`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Host code only.

## Acceptance criteria

- [x] Samples drawn by it start a run that converges: 2026-10-02, the end-to-end tests
      in `DifferentialEvolutionOptimizerTests` (local, OpenCL `gfx1036`).
- [ ] No unit test of the box, of the argument checks or of the sizes.
- [ ] ⚠ The constructor takes `upperBound` before `lowerBound`, the reverse of the
      CPU package's `WithBounds(lower, upper)`. Swapped arguments are not detected
      (`lower > upper` is accepted) and silently mirror the box.
- [ ] ⚠ Not reproducible: it draws from `Random.Shared` and takes no seed.

## Taboos

- **No change of the constructor's parameter order without a public break.** Both are
  `IEnumerable<double>`; swapping them compiles at every call site and mirrors every
  caller's box.
