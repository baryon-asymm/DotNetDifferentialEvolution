# BOOT.md — UnitTests/Builder

## Purpose

Level U2: what `DifferentialEvolutionBuilder` assembles and what it refuses. The staged
interfaces already enforce the order of calls at compile time; these tests cover the
runtime guards and the composition behind each preset. A preset's parts are read back
from private fields (`DifferentialEvolution._problemContext`, `_workerControllers`,
`WorkerController._algorithmExecutor`, `AlgorithmExecutor._mutationStrategy` and
`_selectionStrategy`) rather than exposed for the test.

## Invariants

- **The presets were pinned before they were refactored.** `DeVariantTests` came with
  `0e2be47`, written ahead of the move to `IDeVariant` so the move could not quietly
  change what the presets build.
- **Tie rules are checked through the assembled preset**, not on `SelectionStrategy`
  alone: the defect guarded is a variant wired to the wrong rule.
- **A reflective read fails with a named error** ("… was renamed; update this test")
  rather than a null reference.
- **Every build here uses one worker and a one-generation (or matching-budget) stop**,
  so no test optimizes anything.

## Dependencies

- [DotNetDifferentialEvolution](../../../src/DotNetDifferentialEvolution/API.md) — the
  builder, `DifferentialEvolution`.
- [Variants](../../../src/DotNetDifferentialEvolution/Variants/API.md) — `IDeVariant`
  and the setup types.
- [Algorithms/Jde](../../../src/DotNetDifferentialEvolution/Algorithms/Jde/API.md),
  [Jade](../../../src/DotNetDifferentialEvolution/Algorithms/Jade/API.md),
  [Shade](../../../src/DotNetDifferentialEvolution/Algorithms/Shade/API.md),
  [Lshade](../../../src/DotNetDifferentialEvolution/Algorithms/Lshade/API.md) — the
  types a preset must install.
- [AlgorithmExecutors](../../../src/DotNetDifferentialEvolution/AlgorithmExecutors/API.md),
  [Controllers](../../../src/DotNetDifferentialEvolution/Controllers/API.md) — read
  reflectively.
- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md),
  [GenerationStrategies](../../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md),
  [LocalSearch](../../../src/DotNetDifferentialEvolution/LocalSearch/API.md),
  [Models](../../../src/DotNetDifferentialEvolution/Models/API.md),
  [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md),
  [MutationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md),
  [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md),
  [SelectionStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/SelectionStrategies/Interfaces/API.md),
  [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md),
  [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md)
  — the parts passed in and asserted on.
- [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/API.md)
  — Sphere.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- A test that builds must dispose (`using var de = …`).

## Acceptance criteria

- [x] Green: 2026-10-03, 54 cases in 3 classes (builder 15, requirements 20,
      variants 19); 53 on 2026-10-02, before the jDE tie row.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Dropping the
      lower-above-upper check in `WithBounds` turned
      `WithBounds_ThrowsWhenLowerExceedsUpper` red; dropping both control-parameter
      guards turned 7 cases red, among them
      `BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider` and
      `AThirdPartyVariantGetsTheSameControlParameterCheckAsABuiltIn`.
- [x] Each of the two guards against a missing F/CR provider is pinned on its own
      (2026-10-03). The builder's guard (`EnsureReadyStateToBuild`) is pinned here by its
      message, which names `WithMutationStrategy(strategy, provider)`. The executor's
      guard, which `Build` also reaches, is pinned in
      [AlgorithmExecutors](../AlgorithmExecutors/BOOT.md). Until then, removing either
      guard alone left all 53 cases green (measured 2026-10-02). Shown red 2026-10-03,
      scratch worktree: the builder's guard disabled alone turns the 6 cases of
      `BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider` red.
- [x] `EachPresetInstallsItsOwnPapersRuleForATie` has a jDE row (2026-10-03): ties
      accepted, the current behaviour. Shown red 2026-10-03: jDE built with
      `acceptsTies: false` turns that row red. The source is still open
      ([Variants](../../../src/DotNetDifferentialEvolution/Variants/BOOT.md)).
- [ ] ⚠ The tests depend on five private field names; a rename fails them by design.

## Taboos

- **No public accessor added for a test.** Read the private field, fail by name.
- **No optimization run here.** Building is the subject; running belongs to the
  integration tests.
