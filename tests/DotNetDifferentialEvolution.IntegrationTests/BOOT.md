# BOOT.md — DotNetDifferentialEvolution.IntegrationTests

## Purpose

What "the CPU engine works as a whole" means: the levels above single parts (those are
[UnitTests](../DotNetDifferentialEvolution.UnitTests/BOOT.md)), what each is checked
against, and what is not covered. Real worker threads, the generation barrier, the
orchestrator, the builder end to end, and convergence on functions with known optima.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| I0 | the executor's generation loop on one thread, seeded | known optima of Sphere and Rosenbrock | ✅ |
| I1 | the worker and orchestrator machinery: a single controller, master and slaves, exception propagation, `NaN` in every best-index scan, ranking maintenance, trial outcomes relayed from the selection strategy | the engine's documented contract; hand-placed `NaN`s; known optima | ✅ (this node) |
| I2 | concurrency: cancellation at the barrier, seeded reproducibility per worker count, convergence under oversubscription, no leaked workers, threads or heap | the documented contract; seed equality; counters and coarse bounds | ✅ ([Concurrency](Concurrency/BOOT.md)) |
| I3 | the builder end to end: every scheme and variant converges, benchmarks, local search, live population size, the guide's examples | literature optima of the shared benchmarks; `docs/AGENT_GUIDE.md` | ✅ ([EndToEnd](EndToEnd/BOOT.md)) |
| Protocol | tree invariant, documents against code | `AGENTS.md`; `tools/protocol-lint` (textual) | partial |

## Invariants

- **Every class carries `Category=Integration`; long ones add `Category=Slow`.** CI's
  second gate runs `Category!=Slow&Category!=Gpu`, so a `Slow` case runs only
  locally. Checked 2026-10-02: 6 cases are `Slow` (`ResourceUsageTests`, four
  `LShadeConvergesOnHarderMultimodalFunctions` cases, one `WorkerControllerTests`
  case). Nothing enforces either mark.
- **The assembly runs its tests one at a time** (`AssemblyInfo.cs`,
  `DisableTestParallelization`): several tests read process-wide counters, thread
  counts and heap size, and concurrent multi-worker runs would oversubscribe the CPU.
- **Every wait is bounded** (`WaitAsync(timeout)`, 30–120 s), so a hang fails instead of
  stalling the suite.
- **Optima come from the shared benchmark library**, never from a run.
- **After the result, a test asserts only what the package promises about the workers**
  (decided 2026-10-27). The result task is completed on the thread of the worker that
  carries the handler (the master; a lone worker is its own), from inside that worker's
  loop. By the [handlers' contract](../../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/API.md)
  (steps 2 and 7) every slave is stopped before the task completes, and `Stop` returns
  only once the thread has left its loop
  ([Controllers](../../src/DotNetDifferentialEvolution/Controllers/API.md)); the worker
  carrying the handler leaves its loop only after `Handle` returns, so its `IsRunning`
  may still be `true` when the awaiting test resumes. Hence:
  - no slave running: asserted **at once**, the first thing after the await;
  - the worker carrying the handler: asserted to have left its loop **within a
    deadline** (`WorkerStopAssert`, [TestSupport](TestSupport/API.md)), never at once;
    the deadline bounds a hang, it is no tolerance on a promise;
  - after `Dispose` nothing runs: promised by the package root's `Dispose`, held where
    a test needs it ([Concurrency](Concurrency/API.md) counts the workers).

  The package does not promise "the result only after every worker has left its loop",
  and a test here does not assume it. Making it a promise would be a contract change of
  the CPU package, the owner's decision (AGENTS.md §11).

## Dependencies

- [DotNetDifferentialEvolution](../../src/DotNetDifferentialEvolution/API.md) — the
  builder.
- [AlgorithmExecutors](../../src/DotNetDifferentialEvolution/AlgorithmExecutors/API.md),
  [Controllers](../../src/DotNetDifferentialEvolution/Controllers/API.md),
  [WorkerControllerEventHandlers](../../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/API.md)
  — driven directly in I0 and I1.
- [ControlParameterProviders](../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md),
  [GenerationStrategies](../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md),
  [LocalSearch](../../src/DotNetDifferentialEvolution/LocalSearch/API.md),
  [Models](../../src/DotNetDifferentialEvolution/Models/API.md),
  [MutationStrategies](../../src/DotNetDifferentialEvolution/MutationStrategies/API.md),
  [MutationStrategies/Interfaces](../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md),
  [SelectionStrategies](../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md),
  [SelectionStrategies/Interfaces](../../src/DotNetDifferentialEvolution/SelectionStrategies/Interfaces/API.md),
  [TerminationStrategies](../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md),
  [TerminationStrategies/Interfaces](../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md),
  [Variants](../../src/DotNetDifferentialEvolution/Variants/API.md) — parts configured
  and observed.
- [FitnessFunctionEvaluators](../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md)
  — the objectives, including the `NaN` and throwing ones.
- [AlgorithmExecutors/Interfaces](../../src/DotNetDifferentialEvolution/AlgorithmExecutors/Interfaces/API.md) — `IAlgorithmExecutor`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [Controllers/WorkerControllerEventHandlers/Interfaces](../../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/Interfaces/API.md) — `IWorkerPassLoopDoneHandler`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [FitnessFunctionEvaluators/Interfaces](../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/Interfaces/API.md) — `ITestFitnessFunctionEvaluator`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [RandomProviders](../../src/DotNetDifferentialEvolution/RandomProviders/API.md) — `SeededRandomProvider`, the command source of `WorkerControllerTests` (2026-10-03, replacing `System.Random` under CA5394).

Outside the tree: xUnit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk
17.14.1 (all since 2026-10-03, for the maximum diagnostics), coverlet.collector 6.0.0.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings from `tests/Directory.Build.props`; references the package and
  [Tests.Common](../DotNetDifferentialEvolution.Tests.Common/API.md) as projects.
- `Environment.ProcessorCount` decides several worker counts, so what runs depends on
  the machine (here 2026-10-02 the multi-worker tests ran with the local core count).

## Acceptance criteria

- [x] Green: 2026-10-02, 76 of 76 cases, Release, local, 15 s; the CI set (without
      `Slow`) 70 of 70 in 11 s; `Slow` alone 6 of 6 in 6 s.
- [x] Every child with tests is non-degenerate: 2026-10-02, in a scratch clone of
      `a504474`, two mutations per child turned it red, and the unmutated clone passed
      76/76. The mutations are listed in each child and below.
- [x] This node's I1 is non-degenerate: 2026-10-02, same clone. Not rebuilding the
      fitness ranking turned all 5 `FitnessRankingMaintenanceTests` cases red; reducing
      the workers' best values with `<` instead of the `NaN` rule turned
      `NaNFitnessTests.CrossWorkerReductionDoesNotReportANaNIndividualAsTheBest` red.
- [ ] ⚠ No test here holds `AlgorithmExecutor`'s own control-parameter guard for a
      hand-built context: `ExecutorFactory` and `ManualAlgorithmRunner` always use the
      legacy `MutationStrategy`, which declares no requirements (searched 2026-10-02).
      Together with the unit finding (the builder's and the executor's guards mask each
      other), the executor's guard is tested by nothing on its own.
- [ ] ⚠ `MultiWorkerHarness.StartAll` says it starts the master first "matching the
      production order"; `DifferentialEvolution.RunAsync` starts the slaves first and
      the master last. Tests driven through the harness run the other order.
- [ ] ⚠ Stale comments in test code: `WorkersOrchestratorTests` says the runs "use the
      thread-safe random provider" (the engine gives each worker its own seeded one);
      [TestSupport](TestSupport/BOOT.md) has the `BuilderOptimizer` case.
- [ ] ⚠ The orchestration tests through the harness are unseeded: a seed passed to
      `ExecutorFactory` is dropped when `workersCount > 1`.
- [ ] ⚠ The stop assertions raced (found 2026-10-27). `WorkersOrchestratorTests` (both
      cases) asserted `Assert.False(harness.AnyRunning)` and `WorkerControllerTests`
      (`ConvergesAndStopsOnTermination`, `PropagatesFitnessFunctionExceptionAndStops`)
      `Assert.False(worker.IsRunning)` the moment the result task completed, which
      includes the worker carrying the handler, still inside its loop at that moment
      (see the invariant above). Seen red in GitHub Actions on Ubuntu, step "Tests
      (excluding slow and GPU)": run 38089616342 (`main`, `cda6cf3`) and the release run
      40489873217 (tag `gpu-v1.2.0`, `de6991f`), both
      `AllWorkersCooperateToConverge`, `Assert.False()` at line 37; PR CI on the same trees
      and every local run passed. Closed when all four cases assert by the invariant
      above and the proof below is recorded here (mutations in a scratch copy only):
      - green: each of the two classes run 200 times in a loop, no failure;
      - the old assertion red under a schedule the contract allows: a pause of 200 ms in
        `WorkerController` between `Handle` returning `shouldTerminate` and the loop's
        exit (M0) turns the old `AllWorkersCooperateToConverge` red and leaves the new
        one green;
      - the new at-once check red on a slave left running: `StopAllWorkers` skipping
        the last slave (M1) turns `AllWorkersCooperateToConverge` red (the exception
        case only when the skipped slave is not the one that threw; record what is seen);
      - the new deadline red on a worker that does not leave its loop: the loop not
        breaking on `shouldTerminate` (M2) turns both convergence cases red; the failure
        path waiting for `Stop` after reporting (M3) turns
        `PropagatesFitnessFunctionExceptionAndStops` red.

## Taboos

- **No unbounded wait.** A hang must fail.
- **No test parallelization in this assembly.** Process-wide measurements need the
  process to themselves.
- **No long test without `Category=Slow`**; no `Slow` test that CI is expected to see.

## Decomposition

- This node: I0 and I1, six classes at the root of the project.
- [Concurrency](Concurrency/API.md) — I2.
- [EndToEnd](EndToEnd/API.md) — I3.
- [TestSupport](TestSupport/API.md) — the runners, harness and assertion shared by
  the three; no tests.
