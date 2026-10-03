# BOOT.md — DotNetDifferentialEvolution (CPU package)

## Purpose

The CPU Differential Evolution package, `DotNetDifferentialEvolution` on nuget.org
(6.0.0 in the csproj, 5.1.0 the latest published): multi-threaded, SIMD-accelerated, classic DE with several
mutation schemes plus jDE, JADE, SHADE and L-SHADE, built through a staged fluent
builder. The objective contract and the solution type come from the shared
`DotNetOptimization.Abstractions` package, so one objective drives every optimizer of
that family.

This node owns the two entry points, `DifferentialEvolutionBuilder` and
`DifferentialEvolution`; everything else is a child.

## Invariants

- **Every public member carries XML documentation.** `GenerateDocumentationFile` is on
  and warnings are errors, so a missing comment fails the build. Held by the build.
- **The public surface is diffed against the last released baseline (5.1.0) on every
  pack.** Deliberate breaks are listed, machine-generated, in
  `CompatibilitySuppressions.xml` (5 entries on 2026-10-03: the `SelectSurvivor` rename
  and the two removed `MutationStrategy` constructors). Held by package validation and
  the CI "Pack" step.
- **Internals are visible to the unit-test assembly only** (`InternalsVisibleTo
  DotNetDifferentialEvolution.UnitTests`), so internal helpers are tested directly
  without becoming public.
- **The builder cannot be finished out of order.** Each call returns the next stage's
  interface; `Build` exists only on the last. The staged interfaces carry the
  authoritative XML documentation, the class members `<inheritdoc />`.
- **`Build` rejects an incoherent configuration before anything runs**, in this order:
  missing stages, a population smaller than the mutation strategy's minimum, a strategy
  that reads F and CR without a provider (it would read `NaN` and finish having
  optimized nothing, silently), then the variant's own `Validate`, last, on a
  configuration the builder already accepted. Held by
  `DifferentialEvolutionBuilderTests.BuildThrowsWhenPopulationIsTooSmallForTheMutationStrategy`,
  `MutationRequirementsValidationTests.BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider`,
  `DeVariantTests.AThirdPartyVariantsValidateRunsAgainstTheCompletedConfiguration`.
- **One seed fixes the whole run for a given worker count.** Worker `k` draws from
  `seed + k`, the generation hook from `seed + W`, the initial sampler from
  `seed + W + 1` (`W` workers); a different `W` is a different run. Held by
  `SeededReproducibilityTests.TheSameSeedReproducesTheRunExactly`,
  `TheSameSeedReproducesTheInitialPopulationToo` and
  `AnAdaptiveVariantIsReproducibleIncludingItsArchiveEviction`.
- **The initial population is evaluated once, single-threaded, through
  `Evaluate(genes)`, and counted.** `EvaluationCount` starts at `N`; the best index and
  the fitness ranking are computed before the first generation. During the run the
  engine calls `Evaluate(workerIndex, genes)` concurrently from every worker.

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (imported as a global using);
.NET 8.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- C# 12, `net8.0`, the repository's full analyzer policy with no package-specific
  relaxations.
- The package ships `README.md` and, under `docs/`, `ALGORITHMS.md` and
  `AGENT_GUIDE.md`; SourceLink with an embedded PDB.
- The objective's thread safety is the caller's: pure, or per-worker state indexed by
  `workerIndex` (`ForFunction` XML docs, `docs/AGENT_GUIDE.md`). The engine does not
  lock around it.

## Acceptance criteria

- [x] The package builds with 0 warnings and its unit and integration suites pass:
      2026-10-02, local run of the CI filters (232 unit, 70 integration); the full
      integration set including `Slow` 76/76.
- [x] The documented examples build and run: 2026-10-02, `DocumentedExampleTests`
      (3 tests).
- [x] Parallel runs converge without data-race corruption, oversubscribed included:
      2026-10-02, `ParallelDeterminismTests` (3 tests).
- [x] Build, run, cancel and dispose leave no workers or threads behind: 2026-10-02,
      `WorkerLifecycleTests` (2), `CancellationTests` (5), `ResourceUsageTests` (1).
- [x] The children are described: 2026-10-02, brownfield slices 3 to 5 (commits
      `1faf888`, `c293ccb`, `c8bf78f`).
- [x] ⚠ The validation baseline was 4.0.0 although 4.1.0 and 5.1.0 shipped, so the
      suppression file folded the 4.1.0 changes in a second time. Closed 2026-10-03:
      baseline 5.1.0, suppression file regenerated (`dotnet pack … -p:ApiCompatGenerateSuppressionFile=true`).
- [ ] ⚠ `WithPopulationUpdateHandler` accepts `null` without a check, unlike every other
      `With…` that takes an object.
- [ ] ⚠ A variant is configured when it is chosen, against the population size and
      bounds of that moment. The staged order makes them final by then; nothing else
      guards it.

## Taboos

- **No `GeneratePackageOnBuild`.** A package from a local branch carries a SourceLink
  map to a commit that may never be pushed (`ecd8f09`, 2026-07-28).
- **No hand-written list of breaking changes.** Regenerate `CompatibilitySuppressions.xml`
  with `ApiCompatGenerateSuppressionFile=true`; it is what release notes are written
  from (csproj comment, `ecd8f09`).
- **No variant-specific knowledge in the builder.** A variant's cross-checks live in its
  own `Validate` (`0e2be47`).
- **No generator shared between workers.** An unseeded run still draws one root seed and
  gives each worker its own (`AlgorithmExecutor` constructor comment).

## Decomposition

Five groups, by role. Only the engine runs threads; everything else is called from it.

- **Engine.** [Controllers](Controllers/API.md) — the worker threads, the generation
  barrier, the orchestrator on the last worker
  ([handlers](Controllers/WorkerControllerEventHandlers/API.md),
  [their contracts](Controllers/WorkerControllerEventHandlers/Interfaces/API.md));
  [AlgorithmExecutors](AlgorithmExecutors/API.md) — one generation's work over one
  worker's stripe ([contract](AlgorithmExecutors/Interfaces/API.md)).
- **Operators.** [MutationStrategies](MutationStrategies/API.md),
  [SelectionStrategies](SelectionStrategies/API.md),
  [ControlParameterProviders](ControlParameterProviders/API.md),
  [PopulationSamplingMaker](PopulationSamplingMaker/API.md).
- **Hooks and state.** [Models](Models/API.md) (`ProblemContext` holds every hook),
  [GenerationStrategies](GenerationStrategies/API.md), [LocalSearch](LocalSearch/API.md),
  [Interfaces](Interfaces/API.md), [TerminationStrategies](TerminationStrategies/API.md).
  `Models` and the hook contracts form the package's one cycle: `ProblemContext` holds
  each hook, and each hook (`GenerationStrategies`, `Interfaces`, `LocalSearch`,
  `TerminationStrategies/Interfaces`) takes a `Models` type (found by a textual estimate
  and confirmed on the code, 2026-10-02; recorded in [Models](Models/BOOT.md)).
  `MutationStrategies` is not in it, although the estimate first said so: it matched
  the property `MutationContext.Population` against the type `Models.Population`.
- **Variants.** [Variants](Variants/API.md) bundles operators with an adaptation from
  `Algorithms/`: [Common](Algorithms/Common/API.md) (archive, ranking),
  [Jde](Algorithms/Jde/API.md), [Jade](Algorithms/Jade/API.md),
  [Shade](Algorithms/Shade/API.md), [Lshade](Algorithms/Lshade/API.md). `Algorithms/`
  holds no code of its own and is not a node.
- **Support.** [RandomProviders](RandomProviders/API.md), [Helpers](Helpers/API.md).

The builder reaches into every group; `DifferentialEvolution` only into the engine and
`Models`.
