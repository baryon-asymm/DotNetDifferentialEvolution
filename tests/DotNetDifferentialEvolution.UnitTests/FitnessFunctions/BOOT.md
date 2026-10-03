# BOOT.md — UnitTests/FitnessFunctions

## Purpose

The convergence tests judge the optimizer against the benchmark library's declared
optima; if a formula or an optimum were wrong, they would judge nothing. This node
checks the library itself: formula and declared optimum agree at the declared
minimizer.

## Invariants

- **The check is consistency, not an independent truth.** It shows that the code and
  the declared optimum agree; the optima themselves come from the literature cited in
  the library ([FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/BOOT.md)).
- **One tolerance for all eleven**: `1e-6 + 1e-4·|f*|`, absolute plus relative, stated
  in the test.

## Dependencies

- [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md)
  — under test, through `BenchmarkFunctionCatalog`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-02, 5 cases in 1 class.
- [x] Non-degenerate: 2026-10-02, scratch clone of `9e3e22d`. Rastrigin's constant
      `10n` changed to `9n` turned both dimensions of
      `EvaluatingAtTheKnownMinimizerReproducesTheGlobalMinimum` red.
- [ ] ⚠ The relative term is not derived: it was sized for Styblinski-Tang's
      approximate optimum and is what lets it pass. Measured 2026-10-02 (slice 6): at
      n = 5 the value at the minimizer is 8.8e-4 below the declared f*, against a
      tolerance of about 0.0196. The other ten functions have f* = 0, where the term
      vanishes.
- [ ] ⚠ Dixon-Price, Schwefel and Himmelblau expose no minimizer and are not checked
      at all here; slice 6 measured them by hand.

## Taboos

- **No widening of the tolerance to admit a function.** Fix the declared optimum or
  record the gap.
