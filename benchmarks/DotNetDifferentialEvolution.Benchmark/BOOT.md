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
- **The throughput benchmark is seeded**; the comparison is not.
- **It builds with the solution**, so a breaking API change fails CI's build step here
  too, though CI never runs it.

## Dependencies

- [DotNetDifferentialEvolution](../../src/DotNetDifferentialEvolution/API.md) — the
  builder, used by the comparison.
- [Models](../../src/DotNetDifferentialEvolution/Models/API.md) — the result read from
  each run.
- [TerminationStrategies](../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — the evaluation limit.

Outside the tree: BenchmarkDotNet 0.14.0; `DotNetOptimization.Abstractions` 1.0.0
(global using from `benchmarks/Directory.Build.props`).

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings from `benchmarks/Directory.Build.props`, which imports the root props so the
  analyzer policy applies here as well; `RollForward=Major` so the net8.0 executable
  starts on a newer runtime.
- Release only; a figure from a Debug build is not a measurement.

## Acceptance criteria

- [x] Builds, 0 warnings: 2026-10-02, `dotnet build -c Release`.
- [x] The comparison runs: 2026-10-02, local, 16 logical processors, one unseeded run,
      2 s in all. Rastrigin: DE/rand/1/bin 1.689E+002, jDE, JADE, SHADE and L-SHADE
      0.000E+000. Ackley: 3.997E-015 for all but SHADE (7.550E-015). One run: these
      are an observation, not a ranking.
- [ ] The BenchmarkDotNet mode was not run in this reconstruction.
- [ ] ⚠ The comparison is unseeded and runs once per cell, so its table cannot be
      reproduced or compared across changes; `WithSeed` exists (`3f3d394`).
- [ ] ⚠ `ConvergenceComparison.cs` imports `DotNetDifferentialEvolution.Interfaces` and
      uses nothing from it.
- [ ] ⚠ Dead or duplicated code in the children: see
      [Functions](Functions/BOOT.md) and [RandomGenerators](RandomGenerators/BOOT.md).

## Taboos

- **No assertion here.** A check that must hold belongs in a test project, where CI
  runs it.
- **No performance claim from a single unseeded run** or from a non-Release build.

## Decomposition

- This node: `Program.cs` (the mode switch) and `ConvergenceComparison`.
- [BenchmarkTesters](BenchmarkTesters/API.md), [Functions](Functions/API.md),
  [RandomGenerators](RandomGenerators/API.md).
