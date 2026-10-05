# BOOT.md — UnitTests/Algorithms

## Purpose

Level U0 for the four adaptive strategies: their parameter updates checked exactly.
The adapted means are private, so the tests read them back by sampling with scripted
draws that make the sampler return its centre: the Gaussian collapses to μ when the
cosine term is 0 (draws `0.5, 0.75`), the Cauchy to its location at the draw `0.5`.

## Invariants

- **Expected values are the papers' formulas evaluated by hand in the test**: JADE's
  `(1 − c)μ + c·mean` with the Lehmer mean for F; SHADE's improvement-weighted
  arithmetic mean (2013, Eq. 17); L-SHADE's weighted Lehmer mean (SHADE 1.1, Algorithm 1)
  and LPSR `round((N_min − N_init)·evals/max + N_init)` rounded half away from zero.
- **SHADE and L-SHADE are pinned on the same inputs** (weights 2 and 4, CR 0.4 and
  0.9), so the arithmetic and Lehmer `M_CR` read as a pair: 0.7333 against 0.8091.
- **Tie handling is pinned in both directions**: jDE inherits on a tie, JADE and SHADE
  learn nothing from one.
- **`TrialRecord`s are built by hand**; no test runs selection to produce them.

## Dependencies

- [Algorithms/Jde](../../../src/DotNetDifferentialEvolution/Algorithms/Jde/API.md),
  [Jade](../../../src/DotNetDifferentialEvolution/Algorithms/Jade/API.md),
  [Shade](../../../src/DotNetDifferentialEvolution/Algorithms/Shade/API.md),
  [Lshade](../../../src/DotNetDifferentialEvolution/Algorithms/Lshade/API.md) — under test.
- [GenerationStrategies](../../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md)
  — `GenerationContext`.
- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — `TrialRecord`,
  `ProblemContext`.
- [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md)
  — `SelectionOutcome`.
- [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — the limits the contexts are built with.
- [Fakes](../../DotNetDifferentialEvolution.Tests.Common/Fakes/API.md),
  [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md),
  [Helpers](../../DotNetDifferentialEvolution.Tests.Common/Helpers/API.md) — scripted
  draws, Sphere, `ProblemContextHelper`.
- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md) — `IControlParameterProvider`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md) — `ITerminationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [FitnessFunctionEvaluators/Interfaces](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/Interfaces/API.md) — `ITestFitnessFunctionEvaluator`. Added 2026-10-03 from the reflection check (`DependencyTests`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 32 cases in 4 classes (jDE 4, JADE 4, SHADE 8, L-SHADE 16).
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. JADE keyed on `Replaced`
      instead of `Improved` turned `AfterGenerationIgnoresATrialAcceptedOnATie` red.
- [ ] ⚠ The sampling bounds (F in `(0, 1]`, CR in `[0, 1]`) of JADE and SHADE are not
      asserted; only the centres are read back.
- [ ] ⚠ `ConstructorValidatesMinimumPopulationSize` checks a minimum below 4 and an
      initial size below the minimum; its comment says the equal case is "handled
      separately", and no test does so.
- [ ] ⚠ Tolerances `1e-9` and `1e-12` are literals in each assertion.

## Taboos

- **No reading the private means by reflection.** Sampling with centre-revealing draws
  checks the same value through the public path the engine uses.
- **No expectation taken from a run.** Each is a formula in the test.
