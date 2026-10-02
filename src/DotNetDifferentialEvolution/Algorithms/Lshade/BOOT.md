# BOOT.md — Algorithms/Lshade

## Purpose

L-SHADE, the CEC-2014 winner: SHADE with SHADE 1.1's two memory rules switched on, plus a
population that shrinks linearly from its initial size to 4 as the evaluation budget is
spent.

## Invariants

- **The schedule is linear in the consumed budget and rounds half up.** Held by
  `LShadeStrategyTests.AfterGeneration_ReducesPopulationLinearlyWithTheEvaluationBudget`
  and `AfterGeneration_RoundsMidpointPopulationSizesHalfUp` (`f7887ab`).
- **The best survive, stored in ascending order.** Held by
  `LShadeStrategyTests.AfterGeneration_KeepsTheBestSurvivorsInAscendingFitnessOrder`.
- **The archive capacity follows the population and rounds half up.** Held by
  `LShadeStrategyTests.AfterGeneration_RoundsAMidpointArchiveCapacityHalfUp`.
- **`M_CR` takes the weighted Lehmer mean, and the terminal rule wins over it.** Held by
  `LShadeStrategyTests.AfterGeneration_UpdatesMemoryCrWithTheWeightedLehmerMean` and
  `AfterGeneration_TerminalCrRuleWinsOverTheLehmerMean` (`e489324`).
- **Arguments that would fail silently are refused at construction** (`c54fa57`). Held
  by `Constructor_RejectsANonPositiveEvaluationBudget`,
  `Constructor_RejectsANegativeArchiveSizeRate`, `Constructor_ValidatesMinimumPopulationSize`.

## Dependencies

- [Common](../Common/API.md) — `AdaptiveStrategyBase.RebuildSortedIndices`, inherited
  through SHADE.
- [Shade](../Shade/API.md) — `ShadeStrategy`, the base class.
- [GenerationStrategies](../../GenerationStrategies/API.md) — `GenerationContext`.
- [Models](../../Models/API.md) — `TrialRecord`.
- [MutationStrategies/Interfaces](../../MutationStrategies/Interfaces/API.md) —
  `MutationRequirements`.

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- Reduction runs in the hook, single-threaded, using the discarded-parents buffer as
  scratch before the next generation writes it.

## Acceptance criteria

- [x] Reduction, rounding, memory rules and argument checks pass: 2026-10-02,
      `LShadeStrategyTests` (9 tests, local run).
- [x] L-SHADE converges on Rosenbrock: 2026-10-02,
      `AdaptiveVariantsConvergenceTests.LShadeConvergesOnRosenbrock`; observers see the
      shrinking live size: `PopulationSizeReportingTests` (integration, full local run).
- [ ] ⚠ The archive capacity is rounded on every resize, where Tanabe's reference
      truncates on resize and rounds only initially (documented divergence, `a88001a`).

## Taboos

- **No zero or negative budget, no negative archive rate.** A zero budget divides to a
  non-finite progress and collapses the population to 4 in the first generation,
  silently (`c54fa57`).
- **No arithmetic mean for `M_CR` here.** It always reports the lower value
  (`mean_WL − mean_WA = Var_w / E_w ≥ 0`), which compounds with the terminal rule and can
  lock a slot at CR = 0 (`e489324`).
