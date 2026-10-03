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
  `LShade_ConvergesOnHarderMultimodalFunctions` cases, one `WorkerControllerTests`
  case). Nothing enforces either mark.
- **The assembly runs its tests one at a time** (`AssemblyInfo.cs`,
  `DisableTestParallelization`): several tests read process-wide counters, thread
  counts and heap size, and concurrent multi-worker runs would oversubscribe the CPU.
- **Every wait is bounded** (`WaitAsync(timeout)`, 30–120 s), so a hang fails instead of
  stalling the suite.
- **Optima come from the shared benchmark library**, never from a run.

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
- [FitnessFunctionEvaluators](../DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/API.md)
  — the objectives, including the `NaN` and throwing ones.
- [AlgorithmExecutors/Interfaces](../../src/DotNetDifferentialEvolution/AlgorithmExecutors/Interfaces/API.md) — `IAlgorithmExecutor`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [Controllers/WorkerControllerEventHandlers/Interfaces](../../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/Interfaces/API.md) — `IWorkerPassLoopDoneHandler`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [FitnessFunctionEvaluators/Interfaces](../DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/Interfaces/API.md) — `ITestFitnessFunctionEvaluator`. Added 2026-10-03 from the reflection check (`DependencyTests`).

Outside the tree: xUnit 2.5.3, xunit.runner.visualstudio 2.5.3, Microsoft.NET.Test.Sdk
17.8.0, coverlet.collector 6.0.0.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings from `tests/Directory.Build.props`; references the package and
  [Tests.Shared](../DotNetDifferentialEvolution.Tests.Shared/API.md) as projects.
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
      `NaNFitnessTests.CrossWorkerReduction_DoesNotReportANaNIndividualAsTheBest` red.
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
