# BOOT.md — Tests.Shared/FitnessFunctionEvaluators

## Purpose

The objectives against which convergence is judged: fourteen standard benchmark
functions with literature domains and optima (the SFU Virtual Library of Simulation
Experiments; Naser et al., WIREs Computational Statistics, 2025, as cited in
`BenchmarkFunctionEvaluator`), plus objectives that return `NaN` or throw on a chosen
evaluation, for the failure paths. If a formula or a declared optimum were wrong, every
convergence test built on it would be meaningless.

## Invariants

- **Every function is minimized and declares a proper box.** Held by
  `BenchmarkFunctionEvaluatorTests.DeclaresWellFormedBounds` (dimensions 2 and 4, all
  fourteen).
- **A function that exposes a minimizer evaluates to its declared minimum there.**
  Held by `BenchmarkFunctionEvaluatorTests.EvaluatingAtTheKnownMinimizerReproducesTheGlobalMinimum`
  (dimensions 2 and 5, the eleven of `WithKnownMinimizer`).
- **The worker overload is the plain one.** Held by
  `BenchmarkFunctionEvaluatorTests.WorkerIndexedEvaluateMatchesPlainEvaluate`.
- **Failure triggers count evaluations, not genes**, atomically, so which evaluation
  fails is fixed even under several workers.
- **A function without a single clean minimizer refuses to name one** rather than name
  an approximate point: tests on it assert the value only.

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Evaluators are pure except the two failing ones, whose only state is an interlocked
  counter; all may be called from several workers at once.

## Acceptance criteria

- [x] Formulas and declared optima checked at the minimizer: 2026-10-02,
      `BenchmarkFunctionEvaluatorTests` (5 cases, unit run).
- [x] Consumers: `BenchmarkConvergenceTests` (through the catalog), `NaNFitnessTests`
      (`NaNSphereEvaluator`), `WorkerControllerTests` and `WorkersOrchestratorTests`
      (`ExceptionRosenbrockEvaluator`), the benchmark project (`SimpleSumEvaluator`):
      2026-10-02, by searching `tests/` and `benchmarks/`.
- [ ] ⚠ Three functions have no minimizer check. Measured 2026-10-02 against this build
      (F# Interactive on `Tests.Shared.dll`): Dixon-Price at its closed-form
      minimizer gives 9.9e-32 (n = 2) and 2.0e-31 (n = 5); Himmelblau gives 0 at
      (3, 2) and ≤ 1.1e-11 at the other three published minima (6-digit coordinates).
      Both agree with f* = 0; no test holds this.
- [ ] ⚠ Schwefel's declared f* = 0 is not attained: with the constant 418.9829 the
      value at xᵢ = 420.968746 is 2.55e-5 (n = 2) and 6.36e-5 (n = 5), measured as above.
      The convergence tolerance (1e-2) absorbs it.
- [ ] ⚠ Styblinski-Tang's declared f* = −39.16599·n lies above the value at the declared
      minimizer: −195.830829 against −195.829950 at n = 5 (difference 8.8e-4), measured
      as above. The unit check passes on its relative term (1e-4·|f*| ≈ 0.02), not on
      agreement; a run can legitimately finish below the "global minimum".

## Taboos

- **No optimum chosen to make a test pass.** Declared values come from the literature;
  a disagreement is recorded here, not tuned away.
- **No gene-dependent failure trigger.** Which evaluation fails must not depend on where
  the search happens to be.
