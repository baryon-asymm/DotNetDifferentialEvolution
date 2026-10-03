# BOOT.md — MutationStrategies

## Purpose

The DE schemes of the CPU package — rand/1, rand/2, best/1, best/2, current-to-best/1,
current-to-pbest/1 and the legacy constant-parameter rand/1 — and the `MutationContext`
they all read. Each strategy only chooses which vectors to combine; the arithmetic,
crossover and repair are shared ([Helpers](Helpers/API.md)).

## Invariants

- **Randomness comes from the context, never from the strategy.** Even the legacy
  `MutationStrategy`, which carries its own F and CR, draws from the worker's provider;
  its constructor takes no provider (the `[Obsolete]` one that ignored it, `3f3d394`, went
  in 6.0.0). Held by the shape of the code.
- **The p-best pool is at least `min(2, N)` and rounds half away from zero**, as in
  Tanabe's reference implementation (`f7887ab`). Held by
  `CurrentToPBestMutationStrategyTests.MutateNeverDrawsPBestFromAPoolSmallerThanTwo`
  and `MutateAddressesThePBestPoolThroughTheFitnessRanking`.
- **Each strategy's `MinimumPopulationSize` covers its distinct draws plus the target.**
  Held by the constants in each class (table in `API.md`).
- **`RandomProvider` and `WorkerRandomProvider` are one object**, so a run's draw order
  does not depend on which path a strategy takes (`ec0fa8f`). Held by the engine, which
  sets both.

## Dependencies

- [RandomProviders](../RandomProviders/API.md) — `SeededRandomProvider` in
  `MutationContext`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`); .NET 8
(`stackalloc`, `Vector<double>` through the helpers).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `MutationContext` is a `ref struct` so the population spans pass without copying; a
  strategy cannot store it.
- Index buffers are `stackalloc`ed; no allocation per trial.

## Acceptance criteria

- [x] The p-best strategy passes its unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/MutationStrategies/CurrentToPBestMutationStrategyTests`,
      local run (part of 115 of 115 unit cases for slice 4).
- [x] best/1, current-to-best/1, rand/2, best/2 and dithered best/1 converge on a
      sphere: 2026-10-02, `MutationStrategyConvergenceTests.EachStrategyConvergesOnSphere`
      (integration, local run, part of 22 of 22). rand/1 (both classes) and
      current-to-pbest/1 are exercised only through the default configuration and the
      adaptive variants' suites.
- [ ] No unit test of the six other strategies' vector choice (only convergence).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [x] `MutationStrategy` takes only the F and CR it uses: 2026-10-03, for 6.0.0. The
      ignored `populationSize`, `lowerBound` and `upperBound` and the `[Obsolete]`
      provider-taking constructor are removed; IDE0290 (primary constructor) and CS9113
      (unread parameter) together allowed no other form under the maximum diagnostics.
- [ ] ⚠ `CurrentToPBestMutationStrategy` draws its indices through the virtual
      `RandomProvider`, unlike the helpers; per trial rather than per gene (cost not
      measured).
- [ ] ⚠ `CurrentToPBestMutationStrategy` lets `x_pbest` coincide with `x_i` or `x_r1`
      (only `r1 ≠ i` and `r2 ∉ {i, r1}` are enforced); JADE's own rule was not checked
      here.

## Taboos

- **No generator held by a strategy.** It would put every worker on one shared stream
  and make `WithSeed` unable to reach the most common configuration (`3f3d394`).
- **No p-best pool of one.** It silently turns current-to-pbest into the greedier
  current-to-best; at L-SHADE's default p 0.11 the library and the reference diverged for
  N 4..13, the whole final phase of every run (`f7887ab`).
- **No banker's rounding.** `Math.Round` defaults to half-to-even; the papers round half
  up (`f7887ab`).
