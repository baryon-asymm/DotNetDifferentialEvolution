# API.md — DotNetDifferentialEvolution.UnitTests

The node exposes nothing outward: nobody references a test project. Its contract
points upward: it is what the CPU package may consider proven part by part.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Crossover, repair, mutation arithmetic and index selection give the closed-form result for scripted draws | U0, [MutationStrategies/Helpers](MutationStrategies/API.md) | ✅ |
| `NaN` ranks worst everywhere: in the sort and in selection | U0, [Helpers](Helpers/API.md), [SelectionStrategies](SelectionStrategies/API.md) | ✅ |
| Survival takes ties unless the variant says not to; success stays strict | U0, [SelectionStrategies](SelectionStrategies/API.md) | ✅ |
| jDE, JADE, SHADE and L-SHADE update their parameters as their papers specify | U0, [Algorithms](Algorithms/API.md) | ✅ |
| The engine's generator is uniform, normal where it should be, seed-determined, with unrelated adjacent seeds | U1, [RandomProviders](RandomProviders/API.md) | ✅ |
| The builder refuses incoherent configurations and assembles each preset as documented | U2, [Builder](Builder/API.md) | ✅ |
| The parameterless `RunAsync` stays in the compiled surface | Surface, this node | ✅ |
| The engine's threads, barrier and cancellation | — | integration tests |

## Children

[Algorithms](Algorithms/API.md), [Builder](Builder/API.md),
[ControlParameterProviders](ControlParameterProviders/API.md),
[FitnessFunctions](FitnessFunctions/API.md), [Helpers](Helpers/API.md),
[Models](Models/API.md), [MutationStrategies](MutationStrategies/API.md) and
[its Helpers](MutationStrategies/Helpers/API.md),
[PopulationSampling](PopulationSampling/API.md), [RandomProviders](RandomProviders/API.md),
[SelectionStrategies](SelectionStrategies/API.md),
[TerminationStrategies](TerminationStrategies/API.md), [TestSupport](TestSupport/API.md).

## Test ✅

```csharp
[Trait("Category", "Unit")]
public class PublicApiCompatibilityTests
{
    public void RunAsyncKeepsAParameterlessOverloadInTheCompiledSurface();
    public void RunAsyncAlsoTakesACancellationToken();
}
```

`RunAsync()` and `RunAsync(CancellationToken)` must both exist: a call written
`RunAsync()` compiles against an optional-parameter overload, so merging them keeps
every test green while removing the method 4.0.0 consumers are bound to (`c1ef12e`).
Package validation at pack time is the real gate; this one fails in the inner loop.
