# API.md — UnitTests/ControlParameterProviders

Nothing outward. What this node proves about the two fixed providers of F and CR.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| The constant provider returns its pair for any index and never draws | `ConstantControlParameterProviderTests` | ✅ |
| The dithered provider maps a draw `u` to `F = min + u·(max − min)` with CR fixed, accepts `min = max`, refuses `min > max` | `DitheredControlParameterProviderTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class ConstantControlParameterProviderTests
{
    public void ReturnsTheSameParametersForEveryIndividual();
}
[Trait("Category", "Unit")]
public class DitheredControlParameterProviderTests
{
    public void SamplesMutationForceWithinRangeFromTheRandomDraw();
    public void ConstructorThrowsWhenMinExceedsMax();
    public void AllowsEqualMinAndMax();
}
```
