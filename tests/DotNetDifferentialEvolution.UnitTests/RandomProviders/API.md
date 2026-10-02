# API.md — UnitTests/RandomProviders

Nothing outward. What this node proves about the engine's randomness.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Box–Muller and inverse-CDF Cauchy give their closed forms for scripted uniforms | `RandomDistributionHelperTests` | ✅ |
| The integer crossover threshold orders exactly as the floating comparison it replaced, with 0 and 1 handled at the ends | `RandomThresholdTests` | ✅ |
| The cached Gaussian is the other half of the same transform, costs no draw, is normal and uncorrelated, and lives on the instance | `SeededRandomProviderGaussianTests` | ✅ |
| `SeededRandomProvider` is seed-determined, unrelated across adjacent seeds, in `[0, 1)`, uniform in `Next`, without stuck bits | `SeededRandomProviderTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class RandomDistributionHelperTests
{
    public void NextGaussian_MatchesBoxMullerClosedForm();
    public void NextGaussian_WithZeroDeviation_ReturnsMean();
    public void NextCauchy_AtMedianDrawReturnsLocation();
    public void NextCauchy_AtUpperQuartileReturnsLocationPlusScale();
}
[Trait("Category", "Unit")]
public class RandomThresholdTests
{
    public void ScalingPreservesTheOrderOfTheComparisonItReplaces();
    public void AProbabilityOfOneAcceptsEveryDraw();
    public void OneIsTheOnlyInRangeValueThatNeedsClamping();
    public void AProbabilityOfZeroAcceptsOnlyTheZeroDraw();
    public void NegativeAndOutOfRangeValuesAreClamped();
}
[Trait("Category", "Unit")]
public class SeededRandomProviderGaussianTests
{
    public void TheCachedValueIsTheOtherHalfOfTheSameTransform();
    public void APairOfDrawsConsumesTwoUniforms_NotFour();
    public void MeanAndDeviationAreApplied();
    public void TheOutputStillMatchesTheNormalDistribution();
    public void ConsecutiveDrawsAreNotCorrelated();
    public void TheCacheTravelsWithTheInstance_NotTheThread();
    public void AThirdPartyProviderStillGetsThePlainTransform();
    public void TheHelperRoutesTheEnginesProviderThroughTheCache();
}
[Trait("Category", "Unit")]
public class SeededRandomProviderTests
{
    public void SameSeedProducesTheSameStream();
    public void DifferentSeedsProduceDifferentStreams();
    public void NextDoubleStaysInTheUnitInterval();
    public void NextIsUniformOverTheRequestedRange(int maxValue);
    public void NextRejectsANegativeBound();
    public void RawOutputHasNoStuckBits();
}
```
