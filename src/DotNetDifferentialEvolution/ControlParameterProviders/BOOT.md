# BOOT.md — ControlParameterProviders

## Purpose

Separates "which F and CR does this trial use" from "how is the trial built". The
engine asks the provider for each individual and hands the pair to the mutation
strategy, so the same operator serves classic DE (constant), dithered DE and the
self-adaptive variants, whose strategy objects implement this contract themselves. This
node holds the contract and the two stateless providers.

## Invariants

- **Randomness comes from the worker's provider passed in.** The two providers here
  own no generator, which is what keeps a seeded run reproducible. Held by the shape of
  the code; the contract hands the provider in but cannot forbid an implementation
  from owning another.
- **Constant gives the same pair to every individual.** Held by
  `ConstantControlParameterProviderTests.ReturnsTheSameParametersForEveryIndividual`.
- **Dithered F lies in `[min, max)` and is driven by the provider's draw.** Held by
  `DitheredControlParameterProviderTests.SamplesMutationForceWithinRangeFromTheRandomDraw`;
  `min > max` is rejected (`ConstructorThrowsWhenMinExceedsMax`), `min == max` allowed
  (`AllowsEqualMinAndMax`).

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Called on the hot path of every trial: no allocation, no locking.
- A provider shared by all workers must be thread-safe for concurrent calls; the two
  here are (stateless after construction).

## Acceptance criteria

- [x] Both providers pass their unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/ControlParameterProviders`, local run
      (part of 45 of 45 for slice 3).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ No validation of F or CR in either provider: a CR of 1.5 or an F of `NaN` is
      accepted and reaches the crossover.
- [ ] ⚠ The two classes are not sealed though nothing in the repository derives from
      them (checked 2026-10-02 by searching `src`, `tests`, `benchmarks`).

## Taboos

- **No random generator owned by a provider.** It would draw outside the worker's
  stream and break seeded reproducibility (`3f3d394` routed every draw through the
  worker's provider for this reason).
