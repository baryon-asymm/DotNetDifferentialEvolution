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
- **A finite weight can still overflow the sums; the memory stays finite.** Two successes
  over parents the objective scored `double.MaxValue` (the usual "infeasible" sentinel) have
  finite improvements whose sum is `+∞`; `∞/∞` wrote `NaN` into `M_F` and `M_CR`, F and CR
  came out `NaN` and the mutant was all `NaN` (found 2026-10-07 in PastyPropellant, in 5.1.0
  and 6.0.0). When the generation's largest weight `w_max` exceeds `double.MaxValue / (2·N)`
  (`N` the active population size), every weight is divided by `w_max`; the means do not
  depend on the scale, and with `F, CR ≤ 1` no sum then exceeds `N`. Below that bound the
  weights are used as they are, so such a run is bit for bit what 6.0.0 computed. The GPU
  package applies the same rule ([Bookkeeping](../../../DotNetDifferentialEvolution.GPU/Bookkeeping/BOOT.md)).
  Held by `ShadeStrategyTests.AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums`
  and the L-SHADE twin (O1), the `…BelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit`
  pair (O2), `SentinelFitnessTests` (O3).
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
- [x] **O1, overflow (frozen 2026-10-08, before code).** Two improved trials with
      `ParentFf = double.MaxValue`, `TrialFf = 1`, `F = 1`, `CR = 0.9`: the slot's F and CR
      are finite and equal 1.0 and 0.9 (to 1e-12), for SHADE and for L-SHADE. One such
      trial beside one of weight 1 (F 0.8 / CR 0.3 against 0.2 / 0.9): F and CR are the
      large one's. Red on the unfixed code: `CR is NaN` (both classes; the orchestrator's
      draft test, 2026-10-08, kept off the branch). Tests: `ShadeStrategyTests` and its
      `LShadeStrategyTests` twin, the names in the invariant above.
      Done 2026-10-08: red on `a5e579e` with `CR is NaN` in both classes (4 tests run, 2
      red; the mixed-weight pair green); green with the fix (`9d6c9ef`); with the scale never
      applied (divisor always 1.0) the two overflow tests are red again.
- [x] **O2, no change below the bound.** From 200 random sets of two generations of records
      (improvements from tiny to 10³⁰⁵, NaN and infinite parents, ties, kept trials, all-zero
      successful CR in one L-SHADE set of four), the memory of `ShadeStrategy` and of
      `LShadeStrategy` equals 6.0.0's arithmetic (`UnscaledShadeMemory` in the unit tests, a
      copy of it that does not change), bit for bit. Green on the unfixed code, which is that
      arithmetic; red with the scale applied at every bound. Seeds 20261008 (SHADE) and
      20261009 (L-SHADE); the strategy's memory read back exactly through
      `GetControlParameters` (slot drawn as the slot, the Gaussian's uniforms 0, the Cauchy's ½;
      a terminal slot draws no Gaussian).
      Done 2026-10-08: green on `a5e579e` (the arithmetic itself) and on the fix, 200 sets
      each, comparing F and CR of every slot as bit patterns; with the scale applied at
      every bound (divisor the largest weight whenever positive) both tests are red and
      nothing else is.
- [x] **O3, the engine.** An 8-D sphere feasible on `x₀ < −4` and `double.MaxValue`
      elsewhere, population 100, 20 000 evaluations, seed 12345, one worker, SHADE and
      L-SHADE: the objective is given no vector with a `NaN` gene, and the best individual has
      none. Red on the unfixed code: 19 355 (SHADE) and 19 667 (L-SHADE) evaluations with
      `NaN` genes (the orchestrator's draft test, 2026-10-08). Test:
      `IntegrationTests/EndToEnd/SentinelFitnessTests`, through `DifferentialEvolutionBuilder`
      (`WithShade(0.2, 1.0, 100)`, `WithLShade(20 000, 0.11, 2.6, 6)`); the objective counts
      the vectors with a `NaN` gene and scores a `NaN` gene as 0, so that one would win and show.
      Done 2026-10-08: bounds ±5 in every dimension (the criterion leaves them open; these
      reproduce the draft's figures). Red on `a5e579e`: 19 355 (SHADE) and 19 667 (L-SHADE)
      of 20 000 evaluations with a `NaN` gene; green with the fix (none); red again with the
      scale never applied.
- [ ] ⚠ The terminal `M_CR` latches here and does not in Tanabe's `lshade.cc`, whose
      sentinel test is unreachable; measured over 25 runs at the paper's settings, no
      slot became terminal (`a88001a`).

## Taboos

- **No non-finite weight into the sums.** One such record poisoned `M_F` and `M_CR`
  permanently (`68f3a92`).
- **No unscaled sum of weights.** Weights near `double.MaxValue` overflow the five sums
  although each is finite; a check on the weight alone does not see it (found 2026-10-07).
- **No Lehmer `M_CR` in plain SHADE.** Eq. (17) of the 2013 paper is the arithmetic
  mean; the Lehmer mean belongs to L-SHADE (`e489324`).
- **No coupling of the two SHADE 1.1 rules into one switch.** They are separate so that
  each can be turned on and tested alone (`e489324`).
