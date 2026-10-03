# BOOT.md — UnitTests/SelectionStrategies

## Purpose

Level U0 for the selection rule of the DE papers and its two outputs: whether the trial
survives (`f(u) <= f(x)`, or `<` with ties off) and whether it counts as a success
(strictly `<`). The archive and every adaptation read the second; mixing them was
`ae16907`.

## Invariants

- **Every case checks the outcome, the genes written and the fitness written**, at
  individual 1, so an offset error shows.
- **The rules are cited**: SHADE Eq. (6) and L-SHADE Algorithm 2 lines 12 and 16 for
  survival and success; JADE Table I line 20 for ties refused.
- **`NaN` is worse than every real value, and two `NaN`s are not equal.**

## Dependencies

- [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md)
  — `SelectionStrategy`, `SelectionOutcome`; under test.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 9 cases in 1 class.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Ignoring `acceptsTies`
      turned `WithTiesRejectedKeepsTheParentOnEqualFitness` red.
- [x] A third-party strategy's own outcome is what reaches the trial record:
      2026-10-02, integration `TrialOutcomeReportingTests` (3 cases).
      ⚠ Corrected 2026-10-02, slice 8: this item first said a default `SelectTrial`
      member of `ISelectionStrategy` was called by no test. That member was removed in
      `c7c4f1f`; the item was written from history without checking the current code.

## Taboos

- **No test that checks the outcome without the buffers**, or the reverse.
