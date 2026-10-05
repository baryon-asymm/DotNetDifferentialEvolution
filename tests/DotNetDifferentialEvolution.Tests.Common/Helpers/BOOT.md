# BOOT.md — Tests.Common/Helpers

## Purpose

Tests of the executor, the controllers and the adaptive strategies need a live
`ProblemContext` without going through the builder, which would hide the part under
test behind the whole engine. This node builds one from a test objective.

## Invariants

- **A seeded context is reproducible end to end.** The seed fixes the initial
  population and travels on the context, from which the executor derives one generator
  per worker.
- **The context holds a generation strategy only if one is passed.** Supplying one also
  routes the orchestrator's best-individual lookup through the population scan instead
  of the per-worker indices (the method's own remark).

## Dependencies

- [FitnessFunctionEvaluators/Interfaces](../FitnessFunctionEvaluators/Interfaces/API.md)
  — `ITestFitnessFunctionEvaluator`.
- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — `ProblemContext`.
- [GenerationStrategies](../../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md)
  — `IGenerationStrategy`.
- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md)
  — `IControlParameterProvider`, the optional provider (2026-10-03).
- [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md)
  — `ITerminationStrategy`.
- [RandomProviders](../../../src/DotNetDifferentialEvolution/RandomProviders/API.md)
  — `SeededRandomProvider`, `BaseRandomProvider`, the sampling generator (2026-10-03).

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- The context is built through `ProblemContext`'s public constructor and init setters
  only: the package's internals are visible to the unit-test assembly, not to this one.

## Acceptance criteria

- [x] `ProblemContextHelper` is the context source of the integration support
      (`ExecutorFactory`, `ManualAlgorithmRunner`), the four adaptive-strategy unit tests,
      `PopulationViewTests`, `ProblemContextTests` and the benchmark project's
      `SimpleSumTester`: 2026-10-02, by searching `tests/` and `benchmarks/`.
- [ ] ⚠ A seed here does not reproduce a builder run with the same seed: the helper
      samples with `SeededRandomProvider(seed)` (`System.Random(seed)` before
      2026-10-03), the builder with `SeededRandomProvider(seed + W + 1)`.
- [x] No dead code: 2026-10-03, `GenerateBoundsHelper` and the parameterless
      `PopulationHelper.InitializePopulationWithRandomValues` removed. Both had no caller
      in `tests/` or `benchmarks/` (searched 2026-10-02), and the build of the solution
      confirmed it. `PopulationHelper` itself is used only by `ProblemContextHelper`; the
      GPU test project has its own unrelated class of the same name.
- [x] Every public type and member has XML documentation: 2026-10-03, by a build of
      the project with no CS1591.
- [x] ⚠ `PopulationHelper.InitializePopulationWithRandomValues` drew from `System.Random`
      (CA5394) through a public `Random? random` parameter. Closed 2026-10-03 by the
      orchestrator: the parameter is `BaseRandomProvider?` and `ProblemContextHelper`
      passes `SeededRandomProvider(seed)`. Every seeded initial population changed with
      it; the consumer suites were re-run on the new populations, nothing loosened.

## Taboos

- **No call to the builder from here.** A helper that went through the builder would
  test the builder again in every test that uses it.
