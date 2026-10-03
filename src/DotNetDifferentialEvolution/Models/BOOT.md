# BOOT.md — Models

## Purpose

The state of a CPU run and its shapes: `ProblemContext` (everything the engine runs on),
`PopulationView` (a population as flat buffers plus its live and allocated lengths),
`TrialRecord` (what happened to one trial), and `Population` with `IndividualCursor`
(the result a consumer reads without copying).

## Invariants

- **Live count and allocated capacity are kept apart.** `PopulationView.Count` and
  `Population.PopulationSize` are the live individuals, `Capacity` the allocated ones,
  and `GenomeSize` is derived from `Capacity`, so it does not grow as the population
  shrinks. Held by `PopulationViewTests.CapacityIsTheAllocatedLengthAndCountIsTheLiveOne`,
  `PopulationTests.GenomeSizeStaysDerivedFromTheCapacityWhenThePopulationShrinks`.
- **Narrowing the population narrows both buffers at once, and a swap keeps them
  narrowed.** `CurrentPopulationSize`'s setter rewrites both views. Held by
  `PopulationViewTests.NarrowingTheContextNarrowsBothViewsAtOnce` and
  `SwappingKeepsBothViewsNarrowed`.
- **The cursor never points at a dropped individual.** `MoveCursorTo` checks against
  the live size. Held by `PopulationTests.MoveCursorToRefusesAnIndexOutsideTheActivePopulation`.
- **A swap exchanges references, never copies.** Held by
  `ProblemContextTests.SwapPopulationsExchangesCurrentAndTrialBuffers`.
- **The representative population carries the live size, generation, best index and
  evaluation count of the moment it is requested.** Held by
  `ProblemContextTests.GetRepresentativePopulationStampsGenerationBestAndEvaluationCount`
  and the integration tests `PopulationSizeReportingTests`.
- **`TrialRecord` defaults to "nothing happened"** (`ParentKept`). Held by the
  enumeration's zero value in [SelectionStrategies](../SelectionStrategies/API.md).

## Dependencies

- [ControlParameterProviders](../ControlParameterProviders/API.md) —
  `IControlParameterProvider` in the context.
- [GenerationStrategies](../GenerationStrategies/API.md) — `IGenerationStrategy` in the
  context.
- [Interfaces](../Interfaces/API.md) — `IPopulationUpdatedHandler` in the context.
- [LocalSearch](../LocalSearch/API.md) — `ILocalSearchRefiner` in the context.
- [MutationStrategies/Interfaces](../MutationStrategies/Interfaces/API.md) —
  `MutationRequirements` in the context.
- [SelectionStrategies](../SelectionStrategies/API.md) — `SelectionOutcome` in
  `TrialRecord`.
- [TerminationStrategies/Interfaces](../TerminationStrategies/Interfaces/API.md) —
  `ITerminationStrategy` in the context.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`,
`ISolution`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Flat, row-major buffers allocated once per run; no reallocation when the population
  shrinks.

## Acceptance criteria

- [x] The model types pass their unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/Models` (`PopulationTests`,
      `PopulationViewTests`, `ProblemContextTests`, `IndividualCursorTests`), local run,
      part of 115 of 115 unit cases for slice 4.
- [x] Observers see the live size under L-SHADE: 2026-10-02,
      `PopulationSizeReportingTests` (integration, local run, part of 22 of 22).
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ `ProblemContext` sits in a dependency cycle: it holds the hook interfaces of
      `GenerationStrategies`, `Interfaces`, `LocalSearch` and
      `TerminationStrategies/Interfaces`, and each of those takes a `Models` type in its
      signature. The cycle is inside one assembly, so it compiles; it means none of these
      nodes can be described or changed in isolation.
- [ ] ⚠ `ProblemContext` is fully public and its run state is settable: a local-search
      hook receives it whole and can rewrite the best index, the archive or the swap
      (the generation hook was narrowed away from it in `293b2b1`; this one was not).
- [ ] ⚠ Nothing checks that the buffers passed to `ProblemContext` have the lengths its
      sizes claim.
- [ ] ⚠ `Population.IndividualCursor` is one shared mutable object: moving it in a stop
      rule (as `StagnationStreakTerminationStrategy` does) moves it for everyone holding
      the same population.

## Taboos

- **No length taken from a buffer where the live count is meant.** That is TD-3: an
  observer read 50 individuals under L-SHADE of which 4 were live (`8ac954b`,
  measured on a 5-D sphere, 4000 NFE).
- **No narrowing of one population view without the other.** The buffers swap every
  generation; a one-sided narrowing would undo itself at the next swap (`4b8fd78`).
- **No gene arena without its length beside it.** Four independent members whose
  relationship had to be remembered at every use site produced TD-3; `PopulationView`
  carries them together (`4b8fd78`).
