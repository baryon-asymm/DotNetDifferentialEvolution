# BOOT.md — Algorithms/Jade

## Purpose

JADE's adaptation of two means, μF and μCR, toward the parameters of improving trials,
with F from a Cauchy and CR from a normal distribution around them.

## Invariants

- **Only improving trials move the means.** A tie taught nothing (`ae16907`). Held by
  `JadeStrategyTests.AfterGenerationIgnoresATrialAcceptedOnATie`.
- **No success, no change.** Held by
  `JadeStrategyTests.AfterGenerationWithNoSuccessesLeavesMeansUnchanged`.
- **μCR follows the arithmetic mean and μF the Lehmer mean.** Held by
  `JadeStrategyTests.AfterGenerationNudgesMeansTowardSuccessfulParameters`.
- **F is strictly positive and at most 1; CR is in `[0, 1]`.** Held by the redraw loop
  and the clamps (see the ⚠ below).

## Dependencies

- [Common](../Common/API.md) — `AdaptiveStrategyBase` (archive, provider).
- [ControlParameterProviders](../../ControlParameterProviders/API.md) —
  `IControlParameterProvider`.
- [GenerationStrategies](../../GenerationStrategies/API.md) — `IGenerationStrategy`,
  `GenerationContext`.
- [Models](../../Models/API.md) — `TrialRecord`.
- [RandomProviders](../../RandomProviders/API.md) — `RandomDistributionHelper`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- The means are read concurrently by the workers and written only by the hook between
  generations.

## Acceptance criteria

- [x] The adaptation passes its unit tests: 2026-10-02, `JadeStrategyTests` (4 tests,
      local run).
- [x] JADE converges on Rosenbrock: 2026-10-02,
      `AdaptiveVariantsConvergenceTests.SelfAdaptiveVariantsConvergeOnRosenbrock`.
- [ ] ⚠ No argument checks: an adaptation rate outside `[0, 1]` is accepted.
- [ ] ⚠ The sampling bounds (F in `(0, 1]`, CR in `[0, 1]`) are held by the sampling
      loop and the clamps alone; no test asserts them.

## Taboos

- **No engine-wide tie rule imposed on JADE.** JADE keeps the parent on a tie (Table I
  line 20); forcing SHADE's rule moved it off its paper (`a88001a`). The rule lives in
  the variant's selection strategy, not here.
