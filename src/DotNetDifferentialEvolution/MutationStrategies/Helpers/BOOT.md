# BOOT.md — MutationStrategies/Helpers

## Purpose

The hottest code of the library, written once and shared by every strategy: the
difference-vector arithmetic, the distinct-index draw and the per-gene crossover with
repair. Kept apart so the seven strategies stay a few lines each and cannot diverge on
crossover or repair.

## Invariants

- **Binomial crossover always selects the mutant for repair at `jrand`.** Held by
  `CrossoverHelperTests.GuaranteedGeneAlwaysComesFromMutantEvenWhenCrossoverNeverFires`
  and `TheGuaranteedGeneIsUniformlyDistributedOverTheGenome`.
  ⚠ 2026-11-13: the old claim that the trial always differs from its parent overlooked
  equal genes and repair to the parent, as in the NaN case of `RepairsFiniteIntervals`.
- **The inheritance rate matches CR.** Held by
  `CrossoverHelperTests.GeneInheritanceRateMatchesTheClosedForm`.
- **An out-of-bound mutant gene is reflected halfway toward the parent**, the repair of
  the JADE/SHADE/L-SHADE papers. Held by
  `CrossoverHelperTests.RepairReflectsOutOfBoundGenesHalfwayTowardTheParent`.
  With a finite in-box parent, either infinity uses the corresponding bound. Average
  by halving the sum when finite and by adding the halves when it overflows. A NaN
  mutant takes the parent. No repair consumes a draw.
- **Drawn indices are distinct, in range and never the target.** Held by
  `RandomIndexSelectorTests.ProducesDistinctInRangeIndicesNeverEqualToExcluded`,
  `ShiftsCandidatesPastExcludedIndex`, `RetriesUntilCandidateIsDistinct`.
- **The SIMD path equals the scalar one.** Held by
  `MutationMathTests.AssignBasePlusScaledDifferenceMatchesScalarReference`.
- **The per-gene draw is an integer comparison against a threshold scaled once per
  call, through a struct random source.** That keeps the draw inlined (`ec0fa8f`). Held
  by the shape of the code.

## Dependencies

- [MutationStrategies](../API.md) — `MutationContext`, declared in the parent's
  directory.
- [RandomProviders](../../RandomProviders/API.md) — `IRandomSource`,
  `SeededRandomSource`, `ProviderRandomSource`, `RandomThreshold`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Runs once per gene of every trial: no allocation, no virtual call per gene.

## Acceptance criteria

- [x] The helpers pass their unit tests: 2026-10-02,
      `tests/DotNetDifferentialEvolution.UnitTests/MutationStrategies/Helpers`
      (`CrossoverHelperTests`, `MutationMathTests`, `RandomIndexSelectorTests`), local
      run (part of 115 of 115 unit cases for slice 4).
- [x] 2026-11-13, `CrossoverHelperTests.RepairsFiniteIntervals` was red on the
      original positive and negative midpoint overflow and NaN cases. The fixed
      subnormal coordinate also retains its original bits.
      ⚠ 2026-11-13. Previous criterion: “None of these tests has been shown red on a mutation (AGENTS.md §13).”
      The helper test node records an earlier red crossover mutation. F6 adds three
      red cases for repair. Mutation evidence for the other helpers is a separate obligation.
- [ ] Record a red mutation for each of `MutationMathTests` and `RandomIndexSelectorTests`.
- [ ] ⚠ `FillDistinctIndices` loops forever when the population is not larger than the
      number of indices; it relies on the builder's `MinimumPopulationSize` check.

## Verification on 2026-11-13

Git tree `7b7dc4e91f02259ad4b3b84afb6e0c23f7d4248a`, after a Release solution build:
`dotnet test tests/DotNetDifferentialEvolution.UnitTests -c Release --no-build --blame-hang-timeout 4m`
passed 264/264, including `CrossoverHelperTests.RepairsFiniteIntervals`.
All four repair rows passed.

## Taboos

- **No floating-point comparison per gene and no virtual draw per gene.** Crossover was
  78% of trial construction at D 10 and 90% at D 100 (`ec0fa8f`).
- **No second copy of crossover or repair in a strategy.** All seven go through this
  node; a private variant would drift from the papers' rule unseen.
