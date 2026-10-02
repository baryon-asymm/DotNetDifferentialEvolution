# BOOT.md — AlgorithmExecutors

## Purpose

What one worker does in one generation: for its fixed stripe of individuals, build the
trial, evaluate it, select, record. It is where the strategies, the provider, the
objective and the selection rule meet.

## Invariants

- **Striping is fixed: worker `k` handles `k, k+W, …`, end to end.** No individual is
  touched by two workers, so no synchronisation is needed inside a generation, and a
  seeded run does not depend on interleaving (`3f3d394`). Held by the loop; checked by
  `ParallelDeterminismTests` and `SeededReproducibilityTests.TheSameSeedReproducesTheRunExactly`.
- **A seeded run is reproducible for a given worker count, not across counts.**
  Individual `i` draws from worker `i mod W`'s stream (`dcae847` corrected a comment
  that claimed otherwise). Held by the seeding scheme.
- **One generator per worker, even unseeded.** An unseeded run draws one root seed
  instead of sharing `Random.Shared` (`ec0fa8f`). Held by the constructor.
- **The recorded outcome is the selection strategy's own report.** The executor does
  not recompute it (`68f3a92`, `c7c4f1f`). Held by `TrialOutcomeReportingTests`.
- **A configuration without its declared control parameters is refused here too**, for
  contexts assembled without the builder (`7a7649f`). Held by the constructor's check.

## Dependencies

- [ControlParameterProviders](../ControlParameterProviders/API.md) — the context's
  provider, called per trial.
- [Helpers](../Helpers/API.md) — `FitnessComparisonHelper.IsBetter` for the worker's
  best.
- [Models](../Models/API.md) — `ProblemContext`, `TrialRecord`.
- [MutationStrategies](../MutationStrategies/API.md) — `MutationContext`.
- [MutationStrategies/Interfaces](../MutationStrategies/Interfaces/API.md) —
  `IMutationStrategy`, `MutationRequirements`.
- [RandomProviders](../RandomProviders/API.md) — `SeededRandomProvider`.
- [SelectionStrategies](../SelectionStrategies/API.md) — `SelectionOutcome`, the
  selection result stored in the record.
- [SelectionStrategies/Interfaces](../SelectionStrategies/Interfaces/API.md) —
  `ISelectionStrategy`.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Hot path: the trial buffer is `stackalloc`ed per call; no allocation per individual.

## Acceptance criteria

- [x] Seeded runs reproduce bit for bit at a fixed worker count, initial population and
      archive eviction included: 2026-10-02, `SeededReproducibilityTests` (integration,
      full local run).
- [x] Single- and multi-worker runs converge and repeated parallel runs show no
      corruption: 2026-10-02, `ParallelDeterminismTests`, `AlgorithmExecutorConvergenceTests`.
- [x] The record carries the strategy's own outcome: 2026-10-02,
      `TrialOutcomeReportingTests`.
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ Worker seeds are `rootSeed + workerId` in `int` arithmetic; a seed near
      `int.MaxValue` wraps (harmless for SplitMix64, but undocumented).
- [ ] ⚠ The class is public and its constructor takes a `ProblemContext` whose
      consistency nothing checks besides the control-parameter rule.

## Taboos

- **No randomness from anywhere but the worker's own generator.** A shared generator
  makes parallel runs irreproducible and puts a contended indirection on every draw
  (`3f3d394`).
- **No outcome computed here.** The executor used to decide with a hard-coded
  `trial < parent`, which desynchronised the archive and adaptation from any
  non-greedy rule (`68f3a92`).
