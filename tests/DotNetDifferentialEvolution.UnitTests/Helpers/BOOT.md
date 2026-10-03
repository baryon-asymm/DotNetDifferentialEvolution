# BOOT.md — UnitTests/Helpers

## Purpose

Level U0 for the ranking every p-best strategy and L-SHADE's reduction read. Its one
non-obvious rule: .NET's default comparer sorts `NaN` first, which would put a `NaN`
individual into every p-best pool; the package ranks it last.

## Invariants

- **Tied values are asserted as a set, not an order** (`.Order()`), because the sort
  is not stable and the order of equals is not part of the contract.
- **Expected permutations are written out by hand** from the input values.

## Dependencies

- [Helpers](../../../src/DotNetDifferentialEvolution/Helpers/API.md) —
  `PopulationSortHelper`, under test.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 6 cases in 1 class.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Sorting on the raw value
      instead of mapping `NaN` to `+∞` turned `RanksANaNIndividualLastRatherThanBest`
      and `RanksTheRealValuesCorrectlyWhenSeveralAreNaN` red.
- [ ] ⚠ `FitnessComparisonHelper` (the same `NaN` rule for single comparisons) has no
      test of its own; it is reached through selection
      ([SelectionStrategies](../SelectionStrategies/BOOT.md)).

## Taboos

- **No assertion on the order of tied values.**
