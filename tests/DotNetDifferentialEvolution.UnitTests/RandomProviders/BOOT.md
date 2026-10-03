# BOOT.md — UnitTests/RandomProviders

## Purpose

Levels U0 and U1 for the package's only source of randomness during a run: the
per-worker `SeededRandomProvider` (xoshiro256**), its cached Gaussian, the Gaussian and
Cauchy samplers, and the integer threshold the crossover compares against.

## Invariants

- **Closed forms for exact cases**: `z = √(−2 ln u₁)·cos(2π u₂)`; Cauchy
  `location + scale·tan(π(u − 0.5))`; threshold `Scale(1 − 2⁻⁵³) = 2⁶⁴ − 2¹¹`.
- **Statistical cases have explicit bounds and fixed seeds**: Kolmogorov-Smirnov at
  `1.95/√n` (about the 0.999 quantile) against an `erf` approximation accurate to about
  1.5e-7 (Abramowitz & Stegun 7.1.26); chi-square at `df + 4·√(2·df)`; pair correlation
  within `4/√n`; mean and variance within 0.03 and 0.06 over 200 000 draws.
- **Adjacent seeds are tested because the engine uses them**: workers get `seed + k`.
- **The threshold's equivalence is tested on 200 000 random pairs** against the
  floating comparison it replaced.

## Dependencies

- [RandomProviders](../../../src/DotNetDifferentialEvolution/RandomProviders/API.md) —
  under test (`RandomThreshold` is internal).
- [Fakes](../../DotNetDifferentialEvolution.Tests.Common/Fakes/API.md) —
  `ScriptedRandomProvider`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 26 cases in 4 classes (distribution 4, threshold 5,
      Gaussian 8, provider 9).
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Never caching the spare
      normal turned `APairOfDrawsConsumesTwoUniformsNotFour` and
      `TheCachedValueIsTheOtherHalfOfTheSameTransform` red.
- [ ] ⚠ The statistical cases ran red only for the caching mutation's draw-count side;
      none of the KS, chi-square or correlation bounds was itself seen red.
- [ ] ⚠ `RandomThresholdTests.ScalingPreservesTheOrderOfTheComparisonItReplaces`
      draws its inputs from `System.Random(4242)`, not from the provider under test.

## Taboos

- **No unseeded statistical test.**
- **No bound changed without restating its quantile.**
