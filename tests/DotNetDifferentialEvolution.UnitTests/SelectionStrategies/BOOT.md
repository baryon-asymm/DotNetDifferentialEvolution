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
- [ ] ⚠ The default `SelectTrial` member of `ISelectionStrategy` (the bridge for
      third-party strategies, `68f3a92`) is called by no test in `tests/` (searched
      2026-10-02).

## Taboos

- **No test that checks the outcome without the buffers**, or the reverse.
