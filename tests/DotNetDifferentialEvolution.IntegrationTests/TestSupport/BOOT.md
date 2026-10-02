# BOOT.md — IntegrationTests/TestSupport

## Purpose

Three ways to run the engine below the builder (one thread, one controller, master and
slaves), one way to run it through the builder several times, and the one assertion
of convergence. It holds no tests.

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
- [FitnessFunctionEvaluators/Interfaces](../../DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/Interfaces/API.md),
  [Helpers](../../DotNetDifferentialEvolution.Tests.Shared/Helpers/API.md) — the test
  objective contract, `ProblemContextHelper`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `internal`; nothing outside this project uses it.

## Acceptance criteria

- [x] Used by the root node, `Concurrency` and `EndToEnd`: 2026-10-02, by search.
- [ ] ⚠ `BestOfAsync` keeps the best of 3 or 4 unseeded attempts. Its summary justifies
      this by "the public builder API seeds itself from `Random.Shared`", which was true
      when it was written; `WithSeed` arrived later (`3f3d394`, 2026-07-28) and the
      convergence tests could now be seeded. As
      written, a variant that converged only one time in four would pass.
- [ ] ⚠ `MultiWorkerHarness.StartAll` starts the master first and calls that the
      production order; production starts it last
      (`DifferentialEvolution.RunAsync`).
- [ ] ⚠ `ExecutorFactory` silently drops a seed for more than one worker; its remark
      says why (reproducibility holds per worker count) but callers are not told.

## Taboos

- **No assertion with an expected value written by the caller.** The evaluator is the
  reference.
