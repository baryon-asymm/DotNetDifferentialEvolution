# BOOT.md — Algorithms/Lshade

## Purpose

L-SHADE, the CEC-2014 winner: SHADE with SHADE 1.1's two memory rules switched on, plus a
population that shrinks linearly from its initial size to 4 as the evaluation budget is
spent.

## Invariants

- **The schedule is linear in the consumed budget and rounds half up.** Held by
  `LShadeStrategyTests.AfterGenerationReducesPopulationLinearlyWithTheEvaluationBudget`
  and `AfterGenerationRoundsMidpointPopulationSizesHalfUp` (`f7887ab`).
- **The best survive, stored in ascending order.** Held by
  `LShadeStrategyTests.AfterGenerationKeepsTheBestSurvivorsInAscendingFitnessOrder`.
- **The archive capacity follows the population and rounds half up.** Held by
  `LShadeStrategyTests.AfterGenerationRoundsAMidpointArchiveCapacityHalfUp`.
- **`M_CR` takes the weighted Lehmer mean, and the terminal rule wins over it.** Held by
  `LShadeStrategyTests.AfterGenerationUpdatesMemoryCrWithTheWeightedLehmerMean` and
  `AfterGenerationTerminalCrRuleWinsOverTheLehmerMean` (`e489324`).
- **The memory stays finite when the improvements overflow the sums** — the base class's
  rule ([Shade](../Shade/BOOT.md), O1–O3), held for L-SHADE's Lehmer `M_CR` by
  `LShadeStrategyTests.AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums`,
  `AfterGenerationWeighsAnImprovementNearTheLargestDoubleAgainstOneOfOne` and
  `AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit`.
- Both Lehmer means use [SHADE's weight scale](../Shade/BOOT.md), including for subnormal
  improvements and differences of finite fitness values larger than `double.MaxValue`.
- **Arguments that would fail silently are refused at construction** (`c54fa57`). Held
  by `ConstructorRejectsANonPositiveEvaluationBudget`,
  `ConstructorRejectsANegativeArchiveSizeRate`, `ConstructorValidatesMinimumPopulationSize`.

## Dependencies

- [Common](../Common/API.md) — `AdaptiveStrategyBase.RebuildSortedIndices`, inherited
  through SHADE.
- [Shade](../Shade/API.md) — `ShadeStrategy`, the base class.
- [GenerationStrategies](../../GenerationStrategies/API.md) — `GenerationContext`.
- [Models](../../Models/API.md) — `TrialRecord`.
- [MutationStrategies/Interfaces](../../MutationStrategies/Interfaces/API.md) —
  `MutationRequirements`.
- [ControlParameterProviders](../../ControlParameterProviders/API.md) — `IControlParameterProvider`. Added 2026-10-03 from the reflection check (`DependencyTests`).

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- Reduction runs in the hook, single-threaded, using the discarded-parents buffer as
  scratch before the next generation writes it.

## Acceptance criteria

- [x] 2026-11-13, `ShadeStrategyTests.ExtremeProductsRetainRepresentableMeans`: both Lehmer
      means recover constant tiny parameters and the mixed ½ result. A positive CR of ε
      among signed-zero CR values keeps the slot nonterminal.
- [x] F7, 2026-11-13: the L-SHADE rows of `ShadeStrategyTests.ExtremeImprovementsRetainTheirRelativeWeights`
      give (F, CR) = (7/10, 1/2) for weights ε and 3ε, and (19/28, 11/20) for MaxValue and 2·MaxValue.
- [x] Reduction, rounding, memory rules and argument checks pass: 2026-10-02,
      `LShadeStrategyTests` (9 tests, local run).
- [x] L-SHADE converges on Rosenbrock: 2026-10-02,
      `AdaptiveVariantsConvergenceTests.LShadeConvergesOnRosenbrock`; observers see the
      shrinking live size: `PopulationSizeReportingTests` (integration, full local run).
- [ ] ⚠ The archive capacity is rounded on every resize, where Tanabe's reference
      truncates on resize and rounds only initially (documented divergence, `a88001a`).

## Verification on 2026-11-13

Git tree `7b7dc4e91f02259ad4b3b84afb6e0c23f7d4248a`, after a Release solution build:
`dotnet test tests/DotNetDifferentialEvolution.UnitTests -c Release --no-build --blame-hang-timeout 4m`
passed 264/264, including `ShadeStrategyTests.ExtremeProductsRetainRepresentableMeans` and `ShadeStrategyTests.ExtremeImprovementsRetainTheirRelativeWeights`.
Both L-SHADE product and improvement checks passed, including the subnormal and overflow improvement rows.

## Taboos

- **No zero or negative budget, no negative archive rate.** A zero budget divides to a
  non-finite progress and collapses the population to 4 in the first generation,
  silently (`c54fa57`).
- **No arithmetic mean for `M_CR` here.** It always reports the lower value
  (`mean_WL − mean_WA = Var_w / E_w ≥ 0`), which compounds with the terminal rule and can
  lock a slot at CR = 0 (`e489324`).
