# API.md — UnitTests/AlgorithmExecutors

Nothing outward. What this node proves about `AlgorithmExecutor`'s own guard.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| A hand-built context with no provider is refused for rand/1, best/1 and rand/2, with a message naming the missing provider | `AHandBuiltContextWithoutAProviderIsRefused` | ✅ |
| The same strategies are accepted when the context has a provider | `TheSameStrategyIsAcceptedWhenTheContextHasAProvider` | ✅ |
| A strategy that carries its own F and CR needs no provider | `AStrategyThatCarriesItsOwnParametersNeedsNoProvider` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class AlgorithmExecutorTests
{
    public void AHandBuiltContextWithoutAProviderIsRefused(string strategyName);
    public void TheSameStrategyIsAcceptedWhenTheContextHasAProvider(string strategyName);
    public void AStrategyThatCarriesItsOwnParametersNeedsNoProvider();
}
```

⚠ 2026-11-13 — Signature verification found that the two provider cases take a strategy
name. The previous declarations showed an `IMutationStrategy` argument.
