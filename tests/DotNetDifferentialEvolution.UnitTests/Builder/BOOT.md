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

- [x] Green: 2026-10-02, 53 cases in 3 classes (builder 15, requirements 20,
      variants 18).
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Dropping the
      lower-above-upper check in `WithBounds` turned
      `WithBounds_ThrowsWhenLowerExceedsUpper` red; dropping both control-parameter
      guards turned 7 cases red, among them
      `BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider` and
      `AThirdPartyVariantGetsTheSameControlParameterCheckAsABuiltIn`.
- [ ] ⚠ Two guards refuse a missing F/CR provider: the builder's in
      `EnsureReadyStateToBuild` and `AlgorithmExecutor`'s constructor, which `Build`
      also reaches. Measured on the same clone: removing either one alone leaves all 53
      green. The suite proves that `Build` refuses, not which guard does; the
      executor's guard for hand-built contexts is not tested here.
- [ ] ⚠ `EachPresetInstallsItsOwnPapersRuleForATie` covers JADE, SHADE and L-SHADE;
      jDE's tie rule is not pinned.
- [ ] ⚠ The tests depend on five private field names; a rename fails them by design.

## Taboos

- **No public accessor added for a test.** Read the private field, fail by name.
- **No optimization run here.** Building is the subject; running belongs to the
  integration tests.
