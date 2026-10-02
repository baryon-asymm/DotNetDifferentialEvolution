# BOOT.md — Algorithms/Jde

## Purpose

jDE's parameter self-adaptation: F and CR travel with the individual, occasionally
regenerated, and are inherited by a trial that takes the individual's place.

## Invariants

- **Inheritance is on survival, not on improvement.** The individual carried forward is
  the trial whenever the trial was taken, ties included (`ae16907`). Held by
  `JdeStrategyTests.AfterGeneration_KeepsParametersOfATrialAcceptedOnATie` and
  `AfterGeneration_KeepsParametersOfSuccessfulTrialsPerIndividual`.
- **Without regeneration the stored pair is returned; with it F is in `[0.1, 1.0)` and
  CR uniform** at the defaults. Held by
  `JdeStrategyTests.WithoutAdaptation_ReturnsTheStoredPerIndividualParameters` and
  `WhenAdaptationTriggers_RegeneratesFWithinRangeAndCrUniformly`.
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
- [ ] ⚠ jDE stays on the engine's default tie rule (a tie survives) without a source:
      Brest (2006) was paywalled with every mirror dead when `a88001a` checked the
      variants against their papers, and moving semantics on a guess was refused.
- [ ] ⚠ No argument checks: probabilities outside `[0, 1]` or a negative range are
      accepted.

## Taboos

- **No inheritance keyed on `Improved`.** After a tie the individual is the trial; its
  parameters must be the trial's (`ae16907`).
