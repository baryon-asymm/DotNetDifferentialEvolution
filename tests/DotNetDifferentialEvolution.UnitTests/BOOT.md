# BOOT.md — DotNetDifferentialEvolution.UnitTests

## Purpose

What "each part of the CPU package is correct on its own" means: the levels of
verification below the engine, what each is checked against, and what is not covered.
The whole-engine level (runs, convergence, threads, cancellation) belongs to the
integration tests ([IntegrationTests](../DotNetDifferentialEvolution.IntegrationTests/BOOT.md)).

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| U0 | arithmetic and rules of single parts: crossover and repair, mutation math, ranking with `NaN`, selection thresholds, adaptation updates, schedules, termination, samplers | closed forms worked out in the test (scripted draws make every draw known); the papers' equations, cited per test | ✅ |
| U1 | statistical properties of the random parts: uniformity, normality, independence, crossover rate, `jrand` uniformity | chi-square and Kolmogorov-Smirnov bounds and binomial standard errors, with fixed seeds | ✅ |
| U2 | what the builder assembles: validation order, presets' composition, archive sizing, variants' contract | the builder's documented contract; private fields read by reflection | ✅ |
| Surface | the compiled public surface where source and binary compatibility differ (`RunAsync` overloads) | the 4.0.0 surface | ✅, partial |
| Protocol | tree invariant, documents against code | `AGENTS.md`; `tools/protocol-lint` (textual); no reflection checks yet | partial |

A green U2 over a red U0 means the builder wires parts that are themselves wrong.

## Invariants

- **Every test class carries `[Trait("Category", "Unit")]`.** CI's fast gate runs
  `--filter "Category=Unit"`; a class without the trait silently drops out of that gate
  (it still runs in the broader one). Checked 2026-10-02: 29 of 29 classes carry it.
  Nothing enforces the mark.
- **The suite is deterministic.** Every random input is scripted, seeded, or used only
  for a property that holds for any draw (`UniformRandomSamplingMakerTests`,
  `LShadeStrategyTests.AfterGenerationKeepsTheBestSurvivorsInAscendingFitnessOrder`).
  Statistical bounds are taken at about the 0.999 quantile *and* run on fixed seeds, so
  they cannot flake.
- **Expected values are derived, not recorded.** Each exact case states its closed
  form in a comment next to the assertion; no expectation was taken from a run.
- **Internals are reached two ways, both deliberate**: `internal` types through
  `InternalsVisibleTo` (the package grants it to this assembly only); `private` fields
  of the engine by reflection in the U2 tests, which throw a named error if a field is
  renamed, rather than widen the public API for a test.

## Dependencies

- [DotNetDifferentialEvolution](../../src/DotNetDifferentialEvolution/API.md) —
  `DifferentialEvolution`, the target of the surface tests.
- [Models](../../src/DotNetDifferentialEvolution/Models/API.md) — `Population`, the
  return type the surface tests pin.

Each child declares the package nodes it checks. Outside the tree: xUnit 2.5.3,
xunit.runner.visualstudio 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, coverlet.collector 6.0.0.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings from `tests/Directory.Build.props` (net8.0, `LangVersion latest`, the root
  analyzer policy); `IsTestProject`; global `using Xunit`.
- References the package and
  [Tests.Common](../DotNetDifferentialEvolution.Tests.Common/API.md) as projects.
- The whole suite runs in well under a second (89 ms on 2026-10-02); a unit test that
  needs a full optimization run belongs in the integration project.

## Acceptance criteria

- [x] Green: 2026-10-02, 232 of 232 cases (153 methods in 29 classes), Release, local,
      89 ms.
- [x] Every child is non-degenerate: 2026-10-02, in a scratch clone of `9e3e22d`, one
      mutation of the guarded code per child turned that child red, and the unmutated
      clone passed 232/232. The mutations and what went red are listed in each child.
- [x] The surface test is non-degenerate: 2026-10-02, same clone; making the parameterless
      `RunAsync` `internal` turned
      `PublicApiCompatibilityTests.RunAsyncKeepsAParameterlessOverloadInTheCompiledSurface`
      red. (Renaming it instead fails the build: a `cref` in the builder's XML
      documentation names `RunAsync()`.)
- [ ] ⚠ The case count depends on the machine: `MutationMathTests.GenomeSizes` derives
      sizes from `Vector<double>.Count` (26 cases here, AVX2), so another CPU runs a
      different set.
- [ ] ⚠ Tolerances are per-class constants or literals (`1e-12`, `1e-9`, statistical
      bounds), not one shared place.
- [ ] ⚠ No unit tests for `Controllers`, `AlgorithmExecutors`, `LocalSearch`,
      `Interfaces`, `GenerationStrategies` or `Variants` as such; they are reached
      through the builder (U2) and the integration project.

## Taboos

- **No test class without `Category=Unit`.** It would leave the fast CI gate unseen.
- **No expectation copied from a run.** Derive it, state the derivation beside it.
- **No widening of the package's API for a test.** Reflection with a named failure, or
  `InternalsVisibleTo`.
- **No unseeded statistical assertion.** A bound that can fail by chance will.

## Decomposition

One child per package area, mirroring `src/DotNetDifferentialEvolution`:
[AlgorithmExecutors](AlgorithmExecutors/API.md) (since 2026-10-03), [Algorithms](Algorithms/API.md), [Builder](Builder/API.md),
[ControlParameterProviders](ControlParameterProviders/API.md),
[FitnessFunctions](FitnessFunctions/API.md) (checks the shared benchmark library, not the
package), [Helpers](Helpers/API.md), [Models](Models/API.md),
[MutationStrategies](MutationStrategies/API.md) and its
[Helpers](MutationStrategies/Helpers/API.md),
[PopulationSampling](PopulationSampling/API.md), [RandomProviders](RandomProviders/API.md),
[SelectionStrategies](SelectionStrategies/API.md),
[TerminationStrategies](TerminationStrategies/API.md); plus
[TestSupport](TestSupport/API.md), which holds no tests. The surface test lives at the
root.
