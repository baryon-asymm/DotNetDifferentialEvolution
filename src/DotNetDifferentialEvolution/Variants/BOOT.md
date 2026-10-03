# BOOT.md — Variants

## Purpose

A DE variant contributes mutation, parameter source, generation hook, selection rule and
archive capacity, and the pieces are not independently meaningful: jDE's adaptation is
noise without the mutation that reads it, an archive is dead weight without a p-best
strategy that draws from it. Hand assembly from separate `With…` calls was a source of
silently mismatched configurations, so a variant hands the builder the whole bundle
(`0e2be47`). The four published variants are instances of the same extension point a
caller can implement.

## Invariants

- **An adaptive variant's provider and hook are one object.** Adapting the parameters
  and supplying them are two views of one state. Held by
  `DeVariantTests.JdeInstallsRandOneWithASingleObjectAsProviderAndGenerationStrategy`,
  `JadeInstallsCurrentToPBestWithAnArchiveSizedFromThePopulation`,
  `ShadeInstallsCurrentToPBestBackedByTheSuccessHistoryMemory`,
  `LShadeInstallsCurrentToPBestWithTheLargerArchiveItsPaperSpecifies`.
- **Each variant carries its own paper's tie rule.** JADE keeps the parent (Table I line
  20); SHADE and L-SHADE let the trial survive; jDE takes the engine default (`a88001a`).
  Held by `DeVariantTests.EachPresetInstallsItsOwnPapersRuleForATie` for all four; the
  jDE row (added 2026-10-03) pins the current behaviour, ties accepted, and cites no paper.
  ⚠ Corrected 2026-10-02, slice 7: this line first said the test held the rule for every
  variant when it had no jDE case. ⚠ Open: no source for jDE's tie rule is cited.
- **Every preset satisfies its own mutation strategy's requirements.** Held by
  `DeVariantTests.EveryPresetSatisfiesItsOwnMutationStrategysRequirements`.
- **A third-party variant takes the built-ins' path**: configured with the problem
  dimensions, validated after the builder's checks, given the same control-parameter and
  minimum-population checks, and the greedy default when it chooses no selection. Held
  by the five `DeVariantTests.AThirdPartyVariant…` tests and
  `AVariantThatChoosesNoSelectionStrategyGetsTheGreedyDefault`.
- **Archive capacities round half up** (`f7887ab`). Held by
  `DifferentialEvolutionBuilderTests.WithJadeRoundsAMidpointArchiveCapacityHalfUp`,
  `WithShade_…` and `WithLShade_…`.

## Dependencies

- [Algorithms/Jde](../Algorithms/Jde/API.md) — `JdeStrategy`.
- [Algorithms/Jade](../Algorithms/Jade/API.md) — `JadeStrategy`.
- [Algorithms/Shade](../Algorithms/Shade/API.md) — `ShadeStrategy`.
- [Algorithms/Lshade](../Algorithms/Lshade/API.md) — `LShadeStrategy`.
- [ControlParameterProviders](../ControlParameterProviders/API.md) —
  `IControlParameterProvider`.
- [GenerationStrategies](../GenerationStrategies/API.md) — `IGenerationStrategy`.
- [MutationStrategies](../MutationStrategies/API.md) — `RandMutationStrategy`,
  `CurrentToPBestMutationStrategy`.
- [MutationStrategies/Interfaces](../MutationStrategies/Interfaces/API.md) —
  `IMutationStrategy`.
- [SelectionStrategies](../SelectionStrategies/API.md) — `SelectionStrategy`.
- [SelectionStrategies/Interfaces](../SelectionStrategies/Interfaces/API.md) —
  `ISelectionStrategy`.
- [TerminationStrategies](../TerminationStrategies/API.md) —
  `LimitEvaluationNumberTerminationStrategy`.
- [TerminationStrategies/Interfaces](../TerminationStrategies/Interfaces/API.md) —
  `ITerminationStrategy`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `Configure` runs once per builder, before the population exists; `Validate` sees the
  completed configuration.
- A variant's defaults are its paper's settings; changing one changes what the variant
  is, not a tuning.

## Acceptance criteria

- [x] The presets and the extension point pass their unit tests: 2026-10-02,
      `DeVariantTests` (13 tests, local run).
- [x] L-SHADE's argument and budget checks: 2026-10-02,
      `DifferentialEvolutionBuilderTests.WithLShadeThrowsWhenEvaluationBudgetIsNotPositive`,
      `WithLShadeThrowsWhenTerminationEvaluationBudgetDoesNotMatch`,
      `WithLShadeBuildsWhenTerminationEvaluationBudgetMatches`.
- [x] Every variant converges: 2026-10-02, `AdaptiveVariantsConvergenceTests`,
      `BenchmarkConvergenceTests.ShadeConvergesOnMultimodalFunctions` and
      `LShadeConvergesOnHarderMultimodalFunctions` (integration, full local run).
- [ ] ⚠ `LShadeVariant.Validate` compares the budget only against
      `LimitEvaluationNumberTerminationStrategy`: under any other stop rule, a run can stop
      with the population far above 4 or spend its tail at 4, unreported.
- [ ] ⚠ The p-best pool floor of 2 (in the mutation strategy) is SHADE's, applied to JADE
      too: a per-variant floor needed a new public parameter, which a patch release could
      not add; `f7887ab` points it at this variant object.
- [ ] ⚠ jDE and JADE do not check their rates, and `JdeVariant` exposes neither tau nor
      the F range: they are reachable only through `JdeStrategy` wired by hand.

## Taboos

- **No tie rule imposed engine-wide on every variant.** Doing so moved JADE off its paper
  (`a88001a`); the rule is part of each variant's bundle.
- **No variant-specific knowledge in the builder.** L-SHADE's budget check moved out of
  the builder into `Validate` (`0e2be47`); a new variant's checks go to its own
  `Validate`.
- **No changing a preset's assembled setup without its characterization tests.** They
  were written before `0e2be47` to pin the presets' composition across the refactor.
