# BOOT.md — UnitTests/MutationStrategies/Helpers

## Purpose

Levels U0 and U1 for `CrossoverHelper`, `MutationMath` and `RandomIndexSelector`, the
internal arithmetic under every scheme. Exact cases script every draw; statistical
cases check that the integer-threshold crossover (which replaced `NextDouble() <= CR`)
is still the same algorithm.

## Invariants

- **Exact cases state their trace.** Each scripted case lists, gene by gene, which draw
  decided what and what the repair produced.
- **The vector code is checked against a scalar loop written in the test**, at genome
  sizes derived from `Vector<double>.Count` (one below, at, one above, twice, twice plus
  three) plus fixed sizes, de-duplicated so xUnit does not silently drop a repeated
  case.
- **Statistical bounds are explicit and seeded**: about 3.5 binomial standard errors
  for the inheritance rate; chi-square at `df + 4·√(2·df)` for `jrand`.

## Dependencies

- [MutationStrategies/Helpers](../../../../src/DotNetDifferentialEvolution/MutationStrategies/Helpers/API.md)
  — under test (internal, through `InternalsVisibleTo`).
- [RandomProviders](../../../../src/DotNetDifferentialEvolution/RandomProviders/API.md)
  — `SeededRandomProvider` and the two random-source adapters.
- [Fakes](../../../DotNetDifferentialEvolution.Tests.Shared/Fakes/API.md) — scripted
  and deterministic providers.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 42 cases in 3 classes (crossover 10, math 26, selector 6).
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Clamping an above-bound
      gene to the bound instead of the midpoint turned
      `MixesMutantAndParentGenesAndRepairsOutOfBounds` and
      `RepairReflectsOutOfBoundGenesHalfwayTowardTheParent` red.
- [ ] ⚠ `MutationMathTests` runs a machine-dependent set: 26 cases at
      `Vector<double>.Count = 4` (this machine); a different width gives different sizes.
- [ ] ⚠ The below-bound repair is checked only on `jrand` (gene 0 of
      `RepairReflectsOutOfBoundGenesHalfwayTowardTheParent`); the mutation probe above
      exercised the above-bound branch.

## Taboos

- **No reference computed with the code under test.** The scalar formula is written
  out in the test.
- **No statistical bound tightened or loosened without restating its quantile.**
