# BOOT.md — Algorithms/Shade

## Purpose

SHADE's adaptation: a memory of successful `(F, CR)` means instead of JADE's single pair,
updated with improvement-weighted means. Two rules from SHADE 1.1 are switchable so
L-SHADE can turn them on and each can be tested alone.

## Invariants

- **Only improving trials with finite parent and trial fitness enter the memory.** A tie has
  zero weight. A NaN or infinite endpoint makes the improvement unmeasurable. Held by
  `ShadeStrategyTests.AfterGenerationIgnoresASuccessWhoseImprovementIsNotMeasurable` and
  `AfterGenerationIgnoresASuccessOverAnInfiniteParent`.
- **A finite weight can still overflow the sums; the memory stays finite.** Two successes
  over parents the objective scored `double.MaxValue` (the usual "infeasible" sentinel) have
  finite improvements whose sum is `+∞`; `∞/∞` wrote `NaN` into `M_F` and `M_CR`, F and CR
  came out `NaN` and the mutant was all `NaN` (found 2026-10-07 in PastyPropellant, in 5.1.0
  and 6.0.0). When the generation's largest weight `w_max` exceeds `double.MaxValue / (2·N)`
  (`N` the active population size) and is representable, every weight is divided by `w_max`.
  If a difference of finite endpoints overflows, compute every weight as
  `(parent/2 − trial/2) / (double.MaxValue/2)`, so a weight can be as large as 2.
  When the largest difference is below 2⁻⁹⁷⁰, express the weights in units of 2⁻¹⁰²²
  before the products with F or CR. Between the two bounds the original arithmetic applies. The GPU
  package applies the same rule ([Bookkeeping](../../../DotNetDifferentialEvolution.GPU/Bookkeeping/BOOT.md)).
  Held by `ShadeStrategyTests.AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums`
  and the L-SHADE twin (O1), the `…BelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit`
  pair (O2), `SentinelFitnessTests` (O3).
  ⚠ 2026-11-13 (F7): the earlier rule discarded finite endpoints whose subtraction overflowed
  and used tiny differences directly. The two cases below reproduced incorrect memory values.
- **Protect the products as well as the differences.** For each requested mean let A and B
  be its ordinary numerator and denominator, W the total weight and m = 2⁻⁹⁶⁹. When
  A ≤ max(N, B)·m or B ≤ W·m, recompute the affected ratio from five sums with independent
  integer exponents. These bounds allow for N lost subnormal terms and for a subnormal
  parameter multiplied by a large weight before division. A zero CR set and an already
  terminal slot need no CR recomputation.
  Each product uses the weight and parameter of the same success. Accumulation aligns
  binary64 significands in record order and the quotient is converted back to binary64.
  Alignment, products and division still round. A true mean below half of ε rounds to zero.
  ⚠ 2026-11-13: weight scaling alone left F = 0 for weights 2⁻⁹⁶⁹ and 2⁻⁹⁶⁸ with F = 2⁻⁵⁴.
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

- [x] 2026-11-13, `ShadeStrategyTests.ExtremeProductsRetainRepresentableMeans`: both
      strategies recover constant parameters 2⁻⁵⁴, 2⁻⁶⁰⁰, 2⁻¹⁰⁰⁰ and ε. For weights
      (1, 2⁻¹⁰⁰⁰) and parameters (2⁻¹⁰⁰⁰, 1) the rounded Lehmer mean is ½ and the
      arithmetic mean is 2⁻⁹⁹⁹. An independent CPU consumer reported 144/144
      constant cases equal and 772/772 mixed cases within 3 ULP of exact rational arithmetic.
      ⚠ 2026-11-13: the consumer was written during verification in this workspace.
      Previous wording and evidence:
      2026-11-13, `ShadeStrategyTests.ExtremeProductsRetainRepresentableMeans`: both
      strategies recover constant parameters 2⁻⁵⁴, 2⁻⁶⁰⁰, 2⁻¹⁰⁰⁰ and ε. For weights
      (1, 2⁻¹⁰⁰⁰) and parameters (2⁻¹⁰⁰⁰, 1) the rounded Lehmer mean is ½ and the
      arithmetic mean is 2⁻⁹⁹⁹. The owner's independent CPU consumer reported 144/144
      constant cases equal and 772/772 mixed cases within 3 ULP of exact rational arithmetic.
- [x] F7, 2026-11-13: `ShadeStrategyTests.ExtremeImprovementsRetainTheirRelativeWeights`.
      For F (¼, ¾) and CR (¾, ¼), improvements (ε, 3ε) give F = 7/10 and SHADE CR = 3/8.
      Improvements (MaxValue, 2·MaxValue) give F = 19/28 and CR = 5/12. Successes with
      an infinite endpoint are present in both cases. Before the fix F was 1 and ¼ respectively.
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
- [x] **O2, ordinary-scale compatibility.** For ordinary weights and products, both
      strategies retain 6.0.0's memory update bit for bit. Re-verified 2026-11-13 with
      `AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit` in both
      `ShadeStrategyTests` and `LShadeStrategyTests`, 200 sets each against `UnscaledShadeMemory`.
      ⚠ 2026-11-13: the intermediate formulation still included tiny products of ordinary
      weights. A representable constant F can have an unrepresentable weighted square.
      Previous criterion and evidence:
      **O2, ordinary-scale compatibility.** For generations using unscaled weights, both
      strategies retain 6.0.0's memory update bit for bit. Re-verified 2026-11-13 with
      `AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit` in both
      `ShadeStrategyTests` and `LShadeStrategyTests`, 200 sets each against `UnscaledShadeMemory`.
      ⚠ 2026-11-13: F7 supersedes the original lower-range claim. The subnormal case gave
      F = 1 with the unscaled products where the weighted mean is 7/10. The prior O2
      wording and its evidence follow in full.
      **O2, no change below the bound.** From 200 random sets of two generations of records
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

## Verification of extreme products

From the repository root, run the fixed-draw unit tests and replay the review matrices
with Python 3.9+ (standard library) and the project's .NET SDK.

```sh
dotnet test tests/DotNetDifferentialEvolution.UnitTests -c Release --filter FullyQualifiedName~ExtremeProductsRetainRepresentableMeans
python tests/DotNetDifferentialEvolution.UnitTests/Algorithms/replay_extreme_products.py
```

The replay reads CPU memory slot zero immediately after `AfterGeneration`. Both means must
equal the common parameter in each of the 144 constant cases. For the 193 mixed sets, both
strategies' F and CR must be finite and within `3 * math.ulp(expected)` of the result rounded
once from an exact rational mean. These 772 comparisons measure the listed inputs. Error for
other generations depends on the number and order of terms.

⚠ 2026-11-13: the verification of tree `7b7dc4e91f02259ad4b3b84afb6e0c23f7d4248a`
previously pointed to `matrix/run.py` and `mixed/check.py` in the saved review materials.
The checkout now carries their inputs, comparison rules and consumer.

## Taboos

- **No non-finite weight into the sums.** One such record poisoned `M_F` and `M_CR`
  permanently (`68f3a92`).
- **No unscaled sum of weights.** Weights near `double.MaxValue` overflow the five sums
  although each is finite; a check on the weight alone does not see it (found 2026-10-07).
- **No Lehmer `M_CR` in plain SHADE.** Eq. (17) of the 2013 paper is the arithmetic
  mean; the Lehmer mean belongs to L-SHADE (`e489324`).
- **No coupling of the two SHADE 1.1 rules into one switch.** They are separate so that
  each can be turned on and tested alone (`e489324`).
