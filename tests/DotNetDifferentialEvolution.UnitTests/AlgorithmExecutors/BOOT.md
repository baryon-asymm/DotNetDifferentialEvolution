# BOOT.md — UnitTests/AlgorithmExecutors

## Purpose

Level U0 for the one rule `AlgorithmExecutor` enforces on its own: a mutation strategy
that reads F and CR from the context is refused when the context has no control-parameter
provider. The builder refuses the same pairing first, so through the builder this guard
is never the one that fires; these tests build the context by hand, the one path where it
is the only guard. Unguarded, such a run completes normally and is silently wrong: NaN
parameters, NaN trials, every trial rejected, and the initial sample reported as the
optimum.

## Invariants

- **The context is built by hand, never by the builder**, through
  `ProblemContextHelper.CreateContext`. A builder path would test the builder's guard.
- **Each refusal has its positive control.** The same strategies are accepted with a
  provider, and the legacy strategy (`MutationRequirements.None`) is accepted without
  one, so a guard that refused everything would fail.

## Dependencies

- [AlgorithmExecutors](../../../src/DotNetDifferentialEvolution/AlgorithmExecutors/API.md)
  — `AlgorithmExecutor`; under test.
- [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md),
  [MutationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md)
  — the strategies paired with the context.
- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md)
  — `ConstantControlParameterProvider`, `IControlParameterProvider`.
- [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md),
  [SelectionStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/SelectionStrategies/Interfaces/API.md),
  [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md),
  [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md),
  [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — the rest of a
  context and an executor, and the parameter types of their constructors.
- [FitnessFunctionEvaluators](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/API.md),
  [FitnessFunctionEvaluators/Interfaces](../../DotNetDifferentialEvolution.Tests.Common/FitnessFunctionEvaluators/Interfaces/API.md),
  [Helpers](../../DotNetDifferentialEvolution.Tests.Common/Helpers/API.md) —
  `SphereEvaluator`, `ProblemContextHelper`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [x] Green: 2026-10-03, 7 cases in 1 class.
- [x] Non-degenerate: 2026-10-03, scratch worktree. With the executor's guard disabled
      alone, the 3 cases of `AHandBuiltContextWithoutAProviderIsRefused` turn red and the
      54 builder cases stay green; that is the gap this node closes.

## Taboos

- **No context from the builder.** It would test the builder's guard, which
  `UnitTests/Builder` already holds.
