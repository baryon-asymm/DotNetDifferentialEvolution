# BOOT.md — MutationStrategies/Helpers

## Purpose

The hottest code of the library, written once and shared by every strategy: the
difference-vector arithmetic, the distinct-index draw and the per-gene crossover with
repair. Kept apart so the seven strategies stay a few lines each and cannot diverge on
crossover or repair.

## Invariants

- **Binomial crossover always takes one gene (`jrand`) from the mutant.** A trial never
  equals its parent. Held by `CrossoverHelperTests.GuaranteedGeneAlwaysComesFromMutantEvenWhenCrossoverNeverFires`
  and `TheGuaranteedGeneIsUniformlyDistributedOverTheGenome`.
- **The inheritance rate matches CR.** Held by
  `CrossoverHelperTests.GeneInheritanceRateMatchesTheClosedForm`.
- **An out-of-bound mutant gene is reflected halfway toward the parent**, the repair of
  the JADE/SHADE/L-SHADE papers. Held by
  `CrossoverHelperTests.RepairReflectsOutOfBoundGenesHalfwayTowardTheParent`.
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
- [ ] None of these tests has been shown red on a mutation (AGENTS.md §13).
- [ ] ⚠ `FillDistinctIndices` loops forever when the population is not larger than the
      number of indices; it relies on the builder's `MinimumPopulationSize` check.

## Taboos

- **No floating-point comparison per gene and no virtual draw per gene.** Crossover was
  78% of trial construction at D 10 and 90% at D 100 (`ec0fa8f`).
- **No second copy of crossover or repair in a strategy.** All seven go through this
  node; a private variant would drift from the papers' rule unseen.
