# BOOT.md — IntegrationTests/EndToEnd

## Purpose

Level I3: the package as a consumer uses it, through `DifferentialEvolutionBuilder`.
The evidence that the algorithms optimize, not only that they run, and that the
documented examples are code that builds.

## Invariants

- **Optima and domains come from the shared benchmark library**
  ([FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/BOOT.md));
  `ConvergenceAssert` reads them from the evaluator.
- **Tolerances step with difficulty**, each written beside its test: 1e-6 for
  unimodal, 1e-4 for SHADE on multimodal, `1e-2·max(1, |f*|)` for the deceptive set,
  1e-3 for the fixed-F schemes on Sphere.
- **The guide's code is compiled here.** Every line of the guide's §1, §3 and §7 code
  blocks appears verbatim in `DocumentedExampleTests`, apart from the `using`, the
  visibility of the nested `Sphere`, the `Console.WriteLine` replaced by an assertion,
  and one comment (line comparison, 2026-10-02). The tests add what the guide leaves to
  the reader (bounds arrays, an objective instance).
- **Every run uses one worker** except the guide's examples, which use
  `UseAllProcessors` as printed.

## Dependencies

- [DotNetDifferentialEvolution](../../../src/DotNetDifferentialEvolution/API.md) — the
  builder.
- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md),
  [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md),
  [MutationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md),
  [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md),
  [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md)
  — the parts configured.
- [Interfaces](../../../src/DotNetDifferentialEvolution/Interfaces/API.md),
  [LocalSearch](../../../src/DotNetDifferentialEvolution/LocalSearch/API.md),
  [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — the observer, the
  refiner, the result.
- [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md)
  — the benchmarks.
- [TestSupport](../TestSupport/API.md) — `BuilderOptimizer`, `ConvergenceAssert`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [FitnessFunctionEvaluators/Interfaces](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/Interfaces/API.md) — `ITestFitnessFunctionEvaluator`. Added 2026-10-03 from the reflection check (`DependencyTests`).

⚠ Corrected 2026-10-03 by the reflection check: this list named `Algorithms/Lshade`
(for `LShadeStrategy.MinimumPopulationSize`, which no source here mentions) and
`SelectionStrategies/Interfaces` (the tests only call `WithDefaultSelectionStrategy()` on
the builder); both removed.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`
in the guide's own `Sphere`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 34 cases in 6 classes (adaptive 4, benchmarks 16 of which 4
      `Slow`, schemes 5, examples 3, local search 2, population size 4).
- [x] Non-degenerate: 2026-10-02, scratch clone of `a504474`. Selection never taking an
      improvement turned all 9 cases of `MutationStrategyConvergenceTests` and
      `AdaptiveVariantsConvergenceTests` red; a shifted local-search cadence turned
      `RefinerRunsOnConfiguredCadenceAndWriteBackSurvivesIntoResult` red.
- [x] Each convergence test is one run seeded with `BuilderOptimizer.Seed` (2026-10-03,
      [TestSupport](../TestSupport/BOOT.md)); it used to keep the best of 3 or 4 unseeded
      attempts, so a variant converging one time in four passed.
- [ ] ⚠ Two code blocks of `docs/AGENT_GUIDE.md` are compiled by no test: the objective
      contract's signatures (§2) and `RunAsync(cancellationToken)` (cancellation).
- [ ] ⚠ `RandMutationStrategy` is reached only through jDE, and
      `CurrentToPBestMutationStrategy` with fixed parameters only in the root node's
      `FitnessRankingMaintenanceTests`.
- [ ] ⚠ The deceptive set's tolerance absorbs the declared-optimum errors measured in
      slice 6: Schwefel 2.55e-5 at n = 2 (measured); Styblinski-Tang about 3.5e-4 at
      n = 2 (estimate: the n = 5 measurement of 8.8e-4 is linear in n). Both are far
      below the tolerance (1e-2 and about 0.78).

## Taboos

- **No tolerance loosened to make a variant pass.** A variant that needs it is a
  finding.
- **No guide example edited here without the guide**, nor the reverse, in separate
  commits.
