# BOOT.md — Tests.Shared/FitnessFunctionEvaluators/Interfaces

## Purpose

A convergence test needs three things from its objective: where to search, what the
answer is, and the objective itself. This contract bundles them, so a run can be built
and its result judged from one object.

## Invariants

- **It extends the package's objective contract and adds nothing to evaluation.** Any
  implementation can be handed to the builder or a `ProblemContext` as it is.
- **The declared optimum is a claim about the formula**, not a target the test is free
  to choose: a convergence test compares against it.

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`IFitnessFunctionEvaluator`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Implemented by every evaluator of the parent node and consumed by the integration
      support (`ConvergenceAssert`, `ExecutorFactory`, `ManualAlgorithmRunner`) and by
      `ProblemContextHelper`: 2026-10-02, checked by searching `tests/` and
      `benchmarks/`.
- [ ] ⚠ The file imports `DotNetDifferentialEvolution.Interfaces` but uses nothing from
      it; the interface it extends comes from the global using of the abstractions.

## Taboos

- **No method added here for one test's convenience.** Every evaluator would have to
  implement it.
