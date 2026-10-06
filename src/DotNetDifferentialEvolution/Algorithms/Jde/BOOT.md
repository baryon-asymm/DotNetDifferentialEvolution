# BOOT.md — Algorithms/Jde

## Purpose

jDE's parameter self-adaptation: F and CR travel with the individual, occasionally
regenerated, and are inherited by a trial that takes the individual's place.

## Invariants

- **Inheritance is on survival, not on improvement.** The individual carried forward is
  the trial whenever the trial was taken, ties included (`ae16907`). Held by
  `JdeStrategyTests.AfterGenerationKeepsParametersOfATrialAcceptedOnATie` and
  `AfterGenerationKeepsParametersOfSuccessfulTrialsPerIndividual`.
- **Without regeneration the stored pair is returned; with it F is in `[0.1, 1.0)` and
  CR uniform** at the defaults. Held by
  `JdeStrategyTests.WithoutAdaptationReturnsTheStoredPerIndividualParameters` and
  `WhenAdaptationTriggersRegeneratesFWithinRangeAndCrUniformly`.
- **Per-individual state is written only between generations.** Workers read
  `_mutationForces[i]` for their own `i`; the hook writes after the barrier. Held by the
  engine's barrier.

## Dependencies

- [ControlParameterProviders](../../ControlParameterProviders/API.md) —
  `IControlParameterProvider`.
- [GenerationStrategies](../../GenerationStrategies/API.md) — `IGenerationStrategy`,
  `GenerationContext`.
- [Models](../../Models/API.md) — `TrialRecord`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- `GetControlParameters` is called concurrently by every worker.

## Acceptance criteria

- [x] The adaptation passes its unit tests: 2026-10-02, `JdeStrategyTests` (4 tests,
      local run).
- [x] jDE converges on Rosenbrock: 2026-10-02,
      `AdaptiveVariantsConvergenceTests.SelfAdaptiveVariantsConvergeOnRosenbrock`
      (integration, full local run).
- [x] ⚠ jDE's tie rule has a source: 2026-10-06, Brest et al. 2006, §III-C (IEEE TEVC
      10(6), p. 646 ff.), "if, and only if, the trial vector yields a better cost function
      value"; `JdeVariant` installs strict selection, held by
      `DeVariantTests.EachPresetInstallsItsOwnPapersRuleForATie`. Until then jDE stayed on
      the engine's default (a tie survives): the paper was paywalled with every mirror dead
      when `a88001a` checked the variants, and moving semantics on a guess was refused.
- [ ] ⚠ No argument checks: probabilities outside `[0, 1]` or a negative range are
      accepted.

## Taboos

- **No inheritance keyed on `Improved`.** Inheritance follows survival (`Replaced`): the
  parameters belong to whichever vector is in the population (`ae16907`). Under jDE's own
  strict selection the two coincide; a custom selection that accepts ties must still pass
  the trial's parameters on with the trial.
