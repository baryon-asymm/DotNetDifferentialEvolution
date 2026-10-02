# BOOT.md — Helpers

## Purpose

One place for "which fitness is better" in the CPU engine. A user objective may return
`NaN`, and IEEE comparisons make a `NaN` individual both irreplaceable and
undisplaceable; this node states the rule that ranks it worst, for pairwise comparison
and for sorting, so every site of the engine ranks the same way.

## Invariants

- **`NaN` is worse than every real value, everywhere.** In pairwise comparison
  (`FitnessComparisonHelper`) and in sorting (`PopulationSortHelper`, `NaN` keyed as
  `+∞`). Held by `PopulationSortHelperTests.RanksANaNIndividualLastRatherThanBest`,
  `RanksTheRealValuesCorrectlyWhenSeveralAreNaN`,
  `StillProducesAValidPermutationWhenEveryValueIsNaN`, and by
  `SelectionStrategyTests` (the comparison is exercised through selection).
- **A tie is not an improvement; two `NaN`s are not a tie.** `IsBetter` is strict;
  `IsBetterOrEqual` admits real ties only. Held by
  `SelectionStrategyTests.TakesTheTrialWhenFitnessIsEqualButDoesNotCallItAnImprovement`
  and `KeepsParentWhenBothFitnessValuesAreNaN`.
- **Sorting does not modify the fitness values.** Keys go to the caller's scratch
  buffer. Held by the shape of the code.

## Dependencies

None.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- No allocation: the sort works in caller-supplied spans.

## Acceptance criteria

- [x] `PopulationSortHelper` passes its unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/Helpers/PopulationSortHelperTests`,
      local run (part of 45 of 45 for slice 3).
- [ ] `FitnessComparisonHelper` has no test of its own; it is checked only through
      `SelectionStrategy`.
- [ ] Every comparison site of the engine goes through these helpers: claimed by
      `66fd1f3` (2026-07-27) for five sites at the time; not re-checked against a
      generated list of `<`/`<=` on fitness values.
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).

## Taboos

- **No plain `<` between fitness values elsewhere in the engine.** That is how `NaN`
  became absorbing: a `NaN` individual could be neither replaced nor displaced as the
  best (`66fd1f3`, 2026-07-27, found on a JADE run reporting best `NaN` while the
  population held 1.24e-19).
- **No default `double` ordering for ranking.** .NET's total order puts `NaN` first,
  which ranked a `NaN` individual best and put it into every p-best pool (`4affd0d`,
  2026-07-27).
- **No treating two `NaN`s as a tie.** It would churn a `NaN` individual's genes every
  generation for nothing (`ae16907`).
