# API.md — DotNetDifferentialEvolution (CPU package)

Namespace: `DotNetDifferentialEvolution`. The CPU package. Its entry points
(`DifferentialEvolutionBuilder`, `DifferentialEvolution`) are recovered in slice 5 of the
root's reconstruction; until then the consumer description is the repository's
`README.md` and `docs/AGENT_GUIDE.md`.

## How the package is used ⏳

To be synthesized in slice 5 from the children's contracts and the builder.

## Children

Described so far:

- [ControlParameterProviders](ControlParameterProviders/API.md) — the source of F and
  CR per trial; constant and dithered providers.
- [Helpers](Helpers/API.md) — the fitness ranking rule (`NaN` worst) and the population
  sort.
- [RandomProviders](RandomProviders/API.md) — the per-worker xoshiro256** generator and
  the Gaussian and Cauchy samplers.
- [SelectionStrategies](SelectionStrategies/API.md) — greedy selection with separate
  survival and success thresholds, and its
  [contract](SelectionStrategies/Interfaces/API.md).
