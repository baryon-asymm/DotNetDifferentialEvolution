# BOOT.md — IntegrationTests/TestSupport

## Purpose

Three ways to run the engine below the builder (one thread, one controller, master and
slaves), one way to run it once through the builder, seeded, and the one assertion of
convergence. It holds no tests.

## Invariants

- **The assertion reads the optimum from the evaluator**, so a test cannot state its
  own expected value.
- **The harness disposes every worker** it created.
- **The manual runner's loop is the orchestrator's, without threads**: execute, swap,
  stamp, check termination.

## Dependencies

- [AlgorithmExecutors](../../../src/DotNetDifferentialEvolution/AlgorithmExecutors/API.md),
  [Controllers](../../../src/DotNetDifferentialEvolution/Controllers/API.md),
  [WorkerControllerEventHandlers](../../../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/API.md)
  — the engine parts wired by hand.
- [GenerationStrategies](../../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md),
  [Models](../../../src/DotNetDifferentialEvolution/Models/API.md),
  [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md),
  [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md),
  [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md)
  — the parts they are built from.
- [FitnessFunctionEvaluators/Interfaces](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/Interfaces/API.md),
  [Helpers](../../DotNetDifferentialEvolution.Tests.Common/Helpers/API.md) — the test
  objective contract, `ProblemContextHelper`.
- [DotNetDifferentialEvolution](../../../src/DotNetDifferentialEvolution/API.md) — `DifferentialEvolution`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [AlgorithmExecutors/Interfaces](../../../src/DotNetDifferentialEvolution/AlgorithmExecutors/Interfaces/API.md) — `IAlgorithmExecutor`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [Controllers/WorkerControllerEventHandlers/Interfaces](../../../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/Interfaces/API.md) — `IWorkerPassLoopDoneHandler`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [MutationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md) — `IMutationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [SelectionStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/SelectionStrategies/Interfaces/API.md) — `ISelectionStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `internal`; nothing outside this project uses it.

## Acceptance criteria

- [x] Used by the root node, `Concurrency` and `EndToEnd`: 2026-10-02, by search.
- [x] One seeded run per convergence test, no best-of-N: 2026-10-03.
      `BestOfAsync` (best of 3 or 4 unseeded attempts, written before `WithSeed`,
      `3f3d394`) is replaced by `RunOnceAsync`, with the seed `BuilderOptimizer.Seed = 1`
      fixed before any run. All 25 EndToEnd convergence cases (the 4 `Slow` ones
      included) pass at seed 1. As a measurement, not a choice, seeds 2 to 21 were each
      run once on the same build (local, Windows 11): all 20 passed
      all 25 cases (500 runs, no failure). That bounds what one seed hides; it is no
      proof for other seeds or engine versions. Still non-degenerate when seeded (2026-10-03,
      scratch worktree): selection never taking an improvement turns all 25 red.
- [x] `MultiWorkerHarness.StartAll` starts the slaves first and the master last, the
      production order (`DifferentialEvolution.RunAsync`): 2026-10-03. It used to start
      the master first while its summary called that the production order. No test was
      shown to depend on the order; this is fidelity to production, not a fix seen red.
- [ ] ⚠ `ExecutorFactory` silently drops a seed for more than one worker; its remark
      says why (reproducibility holds per worker count) but callers are not told.

## Taboos

- **No assertion with an expected value written by the caller.** The evaluator is the
  reference.
