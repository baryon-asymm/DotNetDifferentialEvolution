# API.md — DotNetDifferentialEvolution.Tests.Common

A class library of test support for the CPU package, referenced by
`DotNetDifferentialEvolution.UnitTests`, `DotNetDifferentialEvolution.IntegrationTests`
and `DotNetDifferentialEvolution.Benchmark`. It holds no tests and is not packed. All of
its types live in its children.

## Children

- [Fakes](Fakes/API.md) — scripted and seeded random providers.
- [FitnessFunctionEvaluators](FitnessFunctionEvaluators/API.md) — fourteen benchmark
  functions with declared domains and optima, a catalog, and objectives that return
  `NaN` or throw on a chosen evaluation; their
  [contract](FitnessFunctionEvaluators/Interfaces/API.md).
- [Helpers](Helpers/API.md) — a `ProblemContext` built by hand from a test objective.
