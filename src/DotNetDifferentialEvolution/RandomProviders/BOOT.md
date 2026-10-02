# BOOT.md — RandomProviders

## Purpose

Randomness for the CPU engine: a fast, reproducible generator owned one per worker, the
normal and Cauchy samplers the adaptive variants need, and the struct-typed source that
lets the per-gene crossover draw inline instead of dispatching. It is its own node
because reproducibility and speed of the whole engine both rest on it.

## Invariants

- **Same seed, same stream.** `SeededRandomProvider` is deterministic in its seed;
  SplitMix64 expansion keeps seeds that differ by one (how worker seeds are derived)
  well separated. Held by `SeededRandomProviderTests.SameSeedProducesTheSameStream` and
  `DifferentSeedsProduceDifferentStreams`.
- **One provider per worker, never shared.** The class holds mutable state and the
  Gaussian spare without locking. Reproducibility of a parallel run depends on it
  (`3f3d394`). Held by its use in the engine, not by this node.
- **The Gaussian spare belongs to the instance, not the thread or the class.** Held by
  `SeededRandomProviderGaussianTests.TheCacheTravelsWithTheInstance_NotTheThread`.
- **A probability of exactly 1 accepts every draw.** `RandomThreshold.Scale(1.0)` is
  `ulong.MaxValue`, not an out-of-range conversion. Held by
  `RandomThresholdTests.AProbabilityOfOneAcceptsEveryDraw` and
  `OneIsTheOnlyInRangeValueThatNeedsClamping`.
- **Integer and floating-point Bernoulli tests agree** up to a shared 2^-64 bucket,
  because both sides go through the same monotone scale. Held by
  `RandomThresholdTests.ScalingPreservesTheOrderOfTheComparisonItReplaces`.
- **`SeededRandomProvider` is sealed and `IRandomSource` is taken as a struct type
  argument.** That is what removes virtual dispatch from the per-gene draw. Measured in
  `ec0fa8f` (2026-07-28, idle 36-core machine, one generation, N 300, D 20, one worker):
  68.7 µs against 117.7–126.1 µs before, 1.71–1.84x.

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`); .NET 8
(`BitOperations`, `Math.SinCos`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Hot path: `NextULong` and the `IRandomSource` members are
  `AggressiveInlining`; no allocation per draw.
- A change in how many uniforms a draw consumes changes every seeded run. The README
  promises reproducibility only within a minor version; such a change ships in a new
  minor or major version and is listed in `CHANGELOG.md` (as 5.1.0 lists the switch to
  xoshiro256** and the cached Gaussian).

## Acceptance criteria

- [x] The generator, the threshold and the distributions pass their unit tests:
      2026-10-02, `tests/DotNetDifferentialEvolution.UnitTests/RandomProviders`
      (`SeededRandomProviderTests`, `SeededRandomProviderGaussianTests`,
      `RandomThresholdTests`, `RandomDistributionHelperTests`), local run, together
      with the other slice-3 test classes: 45 of 45 passed.
- [x] The Gaussian output matches the normal distribution: 2026-10-02,
      `SeededRandomProviderGaussianTests.TheOutputStillMatchesTheNormalDistribution`
      (a Kolmogorov–Smirnov test over 200 000 samples, per `550c882`).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ `NextGaussian` and `NextCauchy` accept a negative or `NaN` deviation/scale
      without a check.
- [ ] ⚠ `ProviderRandomSource` synthesizes the raw draw from `NextDouble`, i.e. from 53
      bits; a third-party provider therefore decides crossover with less resolution
      than the engine's own. Intended (it keeps scripted fakes exact), not documented
      to consumers.

## Taboos

- **No shared or static random state.** A static Gaussian cache would race; a
  `[ThreadStatic]` one would untie the stream from the seed (`550c882`). One instance per
  worker is the only safe placement.
- **No `System.Random` behind the engine's provider.** `new Random(seed)` is the
  compatibility-locked subtractive generator and `Random.Shared` is not seedable; both
  were replaced by xoshiro256** in `ec0fa8f` (2026-07-28).
- **No virtual call on the per-gene draw.** Keep the provider sealed and the source a
  struct type argument; crossover was 78% of trial construction at D 10 and 90% at
  D 100, almost all of it the draw (`ec0fa8f`).
- **No change in the number of uniforms a draw consumes inside a minor version.** Every
  seeded run changes with it (`550c882`, listed as breaking in 5.1.0).
