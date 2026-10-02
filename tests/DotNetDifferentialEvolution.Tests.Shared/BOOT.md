# BOOT.md — DotNetDifferentialEvolution.Tests.Shared

## Purpose

What the CPU package's test projects and benchmarks share: the objectives convergence
is judged against, the fakes that make random code assertable, and a way to build an
engine context without the builder. One copy, so the unit tests, the integration tests
and the benchmarks agree on what "Rosenbrock" and its optimum are.

This is a support library, not a tests node in the sense of AGENTS.md §1: it defines no
readiness levels. Those belong to the test projects that use it (slices 7 and 8 of the
root's `## Reconstruction`).

## Invariants

- **It depends on the package's public surface only.** The package's internals are
  visible to the unit-test assembly, not to this one; it references the package as a
  project, not as a NuGet version.
- **It contains no tests and runs none.** It has no test framework reference; a test
  placed here would never run.
- **It is never packed** (`IsPackable=false` from `tests/Directory.Build.props`).

## Dependencies

None.

The node has no code of its own; the package types its children use are declared in
their own `## Dependencies`. Outside the tree: `DotNetOptimization.Abstractions` 1.0.0,
transitively through the package's `ProjectReference`, imported by a global using in
`tests/Directory.Build.props`.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- `net8.0`, `LangVersion latest`, nullable enabled, the repository's analyzer policy:
  all from `tests/Directory.Build.props`, which imports the root props.
- The GPU test project does not reference this library; the GPU package has its own
  test support.

## Acceptance criteria

- [x] Builds with 0 warnings: 2026-10-02, `dotnet build -c Release` of the project.
- [x] Every child is used by at least one consumer, with the exceptions recorded in
      [Helpers](Helpers/BOOT.md): 2026-10-02, by searching `tests/` and `benchmarks/`.
- [ ] ⚠ The taboo on second copies is already broken: the benchmark project defines its
      own `AckleyEvaluator` and `RastriginEvaluator` (`Benchmark/Functions`, bare
      `IFitnessFunctionEvaluator`, no bounds). Their formulas match the ones here as of
      2026-10-02 (read side by side). Recorded for slice 9.

## Taboos

- **No reference to the package's internals,** through reflection or otherwise.
- **No second copy of a benchmark function** in a test project: a function defined
  twice can disagree with itself.

## Decomposition

- [Fakes](Fakes/API.md) — leaf, no dependencies in the tree.
- [FitnessFunctionEvaluators](FitnessFunctionEvaluators/API.md) and its
  [Interfaces](FitnessFunctionEvaluators/Interfaces/API.md) — leaves.
- [Helpers](Helpers/API.md) — the only child that touches the package's engine types,
  and the only one that depends on a sibling.
