# BOOT.md — DotNetDifferentialEvolution.Benchmark

## Purpose

Measurement, not verification: how fast one generation of the engine is
(BenchmarkDotNet), and how good each variant's answer is for an equal evaluation
budget (a printed table). Neither mode asserts anything; correctness and convergence
are the test projects' ([UnitTests](../../tests/DotNetDifferentialEvolution.UnitTests/BOOT.md),
[IntegrationTests](../../tests/DotNetDifferentialEvolution.IntegrationTests/BOOT.md)).

## Invariants

- **Every variant gets the same evaluation budget** in the comparison (300 000): four
  run with a fixed population of 100, L-SHADE with its paper's initial `18·D`.
- **Both modes are seeded.** The comparison since 2026-10-03, with seed 1 on all
  processors, so it repeats on one machine.
- **It builds with the solution**, so a breaking API change fails CI's build step here
  too, though CI never runs it.

## Dependencies

- [DotNetDifferentialEvolution](../../src/DotNetDifferentialEvolution/API.md) — the
  builder, used by the comparison.
- [Models](../../src/DotNetDifferentialEvolution/Models/API.md) — the result read from
  each run.
- [TerminationStrategies](../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — the evaluation limit.
- [FitnessFunctionEvaluators](../../tests/DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md)
  — `RastriginEvaluator`, `AckleyEvaluator`, the comparison's objectives (2026-10-03;
  before, the node's own copies in `Benchmark/Functions`).
- [TerminationStrategies/Interfaces](../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md) — `ITerminationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).

Outside the tree: BenchmarkDotNet 0.14.0; `DotNetOptimization.Abstractions` 1.0.0
(global using from `benchmarks/Directory.Build.props`).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings from `benchmarks/Directory.Build.props`, which imports the root props so the
  analyzer policy applies here as well; `RollForward=Major` so the net8.0 executable
  starts on a newer runtime.
- Release only; a figure from a Debug build is not a measurement.

## Acceptance criteria

- [x] Builds, 0 warnings: 2026-10-02, `dotnet build -c Release`; again under the
      maximum diagnostics, 2026-10-03.
- [x] The comparison runs: 2026-10-02, local, 16 logical processors, one unseeded run,
      2 s in all. Rastrigin: DE/rand/1/bin 1.689E+002, jDE, JADE, SHADE and L-SHADE
      0.000E+000. Ackley: 3.997E-015 for all but SHADE (7.550E-015). One run: these
      are an observation, not a ranking.
- [ ] The BenchmarkDotNet mode was not run in this reconstruction.
- [x] The comparison is seeded: 2026-10-03, `WithSeed(1)` on every variant. It is still
      one run per cell, so a difference between two cells is an observation, not a
      ranking.
- [x] No dead or duplicated code: 2026-10-03. The children `Functions` (second copies of
      the shared Rastrigin and Ackley, same formulas, read side by side 2026-10-02) and
      `RandomGenerators` (`DeterminedRandomProvider`, constructed by nothing) are removed;
      the comparison uses the shared evaluators. The unused import of
      `DotNetDifferentialEvolution.Interfaces` in `ConvergenceComparison.cs` is gone too.

## Taboos

- **No assertion here.** A check that must hold belongs in a test project, where CI
  runs it.
- **No performance claim from a single unseeded run** or from a non-Release build.

## Decomposition

- This node: `ConvergenceComparison`. The mode switch (`Program.cs`) moved to
  [Benchmark.Runner](../DotNetDifferentialEvolution.Benchmark.Runner/BOOT.md) on
  2026-10-03, when this project became a library (CA1515 under the maximum
  diagnostics; BenchmarkDotNet needs public classes).
- [BenchmarkTesters](BenchmarkTesters/API.md). `Functions` and `RandomGenerators`
  were removed on 2026-10-03 as dead or duplicated code.
