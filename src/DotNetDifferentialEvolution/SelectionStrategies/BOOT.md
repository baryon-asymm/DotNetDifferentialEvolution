# BOOT.md — SelectionStrategies

## Purpose

The survival rule of the CPU engine as the DE papers state it: two thresholds, one for
survival and one for success, and a report that keeps them apart. Owns the outcome type
every selection rule returns and the built-in greedy rule; the contract is the child
node.

## Invariants

- **Survival and success are separate thresholds.** By default a tie survives
  (`TrialAccepted`) but only a strict improvement is a success (`TrialImproved`).
  Held by `SelectionStrategyTests.TakesTheTrialWhenFitnessIsEqualButDoesNotCallItAnImprovement`.
- **A trial cannot improve without replacing.** `SelectionOutcome` is one enumeration,
  not two flags. Held by the type.
- **Ties are a property of the variant, not of the engine.** `acceptsTies` defaults to
  `true` (SHADE, L-SHADE); JADE passes `false` (`a88001a`). Held by
  `WithTiesRejectedKeepsTheParentOnEqualFitness` and
  `WithTiesRejectedStillTakesAStrictlyBetterTrial`.
- **`NaN` never wins and is always beaten by a real value**, under both settings; two
  `NaN`s are not a tie. Held by `AcceptsTrialWhenParentFitnessIsNaN`,
  `KeepsParentWhenTrialFitnessIsNaN`, `KeepsParentWhenBothFitnessValuesAreNaN`,
  `WithTiesRejectedAParentScoredNaNIsStillReplaced`.
- **The next population's slot is always written in full** from exactly one source.
  Held by the shape of the code (both branches copy genes and fitness).

## Dependencies

- [Helpers](../Helpers/API.md) — `FitnessComparisonHelper.IsBetter` and
  `IsBetterOrEqual`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Hot path, every individual of every generation: no allocation, spans only.

## Acceptance criteria

- [x] The rule passes its unit tests for strict improvement, worse trial, ties under
      both settings and every `NaN` case: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/SelectionStrategies/SelectionStrategyTests`
      (9 tests), local run (part of 45 of 45 for slice 3).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ jDE and classic DE use the default (ties survive) without a source:
      `a88001a` left them there because Brest (2006) could not be read, "a documented
      gap".
- [ ] ⚠ `genomeSize` is not validated against the spans it slices.

## Taboos

- **No single threshold for survival and success.** Merged, a tie either freezes the
  population on a plateau (strict survival) or drags zero-gain records into the archive
  and the adaptation memory (loose success) (`ae16907`).
- **No engine-wide tie rule.** JADE keeps the parent on a tie and SHADE takes the
  trial; forcing one on both moved JADE off its paper (`a88001a`).
- **No plain `<` on fitness here.** Use the `Helpers` rule, or `NaN` becomes absorbing
  again (`66fd1f3`).
