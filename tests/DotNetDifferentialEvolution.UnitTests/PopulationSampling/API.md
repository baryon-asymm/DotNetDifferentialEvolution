# API.md — UnitTests/PopulationSampling

Nothing outward. What this node proves about uniform initial sampling.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Every gene lands inside its own dimension's bounds | `SamplesEveryGeneWithinItsPerDimensionBounds` | ✅ |
| The whole buffer is written | `FillsTheEntireBuffer` | ✅ |
| Finite intervals give the expected rounded gene with one scripted draw, including an overflowing width and endpoint overshoot | `SamplesFiniteIntervals` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class UniformRandomSamplingMakerTests
{
    public void SamplesEveryGeneWithinItsPerDimensionBounds();
    public void FillsTheEntireBuffer();
    public void SamplesFiniteIntervals(double lower, double upper, double draw, double expected);
}
```
