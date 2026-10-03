# BOOT.md — Algorithms/Shade

## Purpose

SHADE's adaptation: a memory of successful `(F, CR)` means instead of JADE's single pair,
updated with improvement-weighted means. Two rules from SHADE 1.1 are switchable so
L-SHADE can turn them on and each can be tested alone.

## Invariants

- **Only improving trials with a finite improvement enter the memory.** A tie has weight
  zero; a `NaN` or infinite weight is unmeasurable and would poison `M_F`/`M_CR` for
  the rest of the run, since `weightSum <= 0` does not catch `NaN` (`68f3a92`). Held by
  `ShadeStrategyTests.AfterGenerationIgnoresASuccessWhoseImprovementIsNotMeasurable` and
  `AfterGenerationIgnoresASuccessOverAnInfiniteParent`.
- **SHADE (2013) updates `M_CR` with the weighted arithmetic mean** (Eq. 17); the Lehmer
  mean is L-SHADE's (`e489324`). Held by
  `ShadeStrategyTests.AfterGenerationStoresImprovementWeightedMeans`.
- **A terminal slot stays terminal and yields CR = 0.** Held by
  `ShadeStrategyTests.AfterGenerationWithTerminalCrEnabledFixesSlotToZeroWhenAllSuccessfulCrAreZero`
  and `AfterGenerationTerminalCrSlotStaysTerminalEvenAfterNonZeroSuccessfulCr`.
- **No usable success, no change.** Held by
  `ShadeStrategyTests.AfterGenerationWithNoSuccessesLeavesMemoryUnchanged`.

## Dependencies

- [Common](../Common/API.md) — `AdaptiveStrategyBase`.
- [ControlParameterProviders](../../ControlParameterProviders/API.md) —
  `IControlParameterProvider`.
- [GenerationStrategies](../../GenerationStrategies/API.md) — `IGenerationStrategy`,
  `GenerationContext`.
- [Models](../../Models/API.md) — `TrialRecord`.
- [RandomProviders](../../RandomProviders/API.md) — `RandomDistributionHelper`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../../BOOT.md)). In addition:

- The memory is read concurrently by the workers and written only between generations.

## Acceptance criteria

- [x] The memory update passes its unit tests: 2026-10-02, `ShadeStrategyTests` (8
      tests, local run).
- [x] SHADE converges on Rosenbrock: 2026-10-02,
      `AdaptiveVariantsConvergenceTests.SelfAdaptiveVariantsConvergeOnRosenbrock`.
- [ ] ⚠ The terminal `M_CR` latches here and does not in Tanabe's `lshade.cc`, whose
      sentinel test is unreachable; measured over 25 runs at the paper's settings, no
      slot became terminal (`a88001a`).

## Taboos

- **No non-finite weight into the sums.** One such record poisoned `M_F` and `M_CR`
  permanently (`68f3a92`).
- **No Lehmer `M_CR` in plain SHADE.** Eq. (17) of the 2013 paper is the arithmetic
  mean; the Lehmer mean belongs to L-SHADE (`e489324`).
- **No coupling of the two SHADE 1.1 rules into one switch.** They are separate so that
  each can be turned on and tested alone (`e489324`).
