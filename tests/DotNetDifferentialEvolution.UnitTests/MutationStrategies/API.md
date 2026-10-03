# API.md — UnitTests/MutationStrategies

Nothing outward. What this node proves about `CurrentToPBestMutationStrategy`.
The shared arithmetic of every scheme is in [Helpers](Helpers/API.md).

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Both constructors accept rates in `(0, 1]` and refuse others; the range one refuses `min > max` | the five constructor tests | ✅ |
| The p-best pool is `max(round(p·N), 2)` rounded half up: at p = 0.11, 2 for N = 4, 5, 8, 10, 13, 14, 20 and 11 for N = 100 | `MutateNeverDrawsPBestFromAPoolSmallerThanTwo` | ✅ |
| The p-best draw addresses the fitness ranking, not raw population indices | `MutateAddressesThePBestPoolThroughTheFitnessRanking` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class CurrentToPBestMutationStrategyTests
{
    public void ConstructorThrowsWhenPBestRateIsOutOfRange(double pBestRate);
    public void ConstructorAcceptsRatesInTheHalfOpenInterval(double pBestRate);
    public void RangeConstructorThrowsWhenRatesAreOutOfRange(double pBestRateMin, double pBestRateMax);
    public void RangeConstructorThrowsWhenMinExceedsMax();
    public void RangeConstructorAcceptsAValidPerIndividualRange();
    public void MutateNeverDrawsPBestFromAPoolSmallerThanTwo(int populationSize, int expectedPoolSize);
    public void MutateAddressesThePBestPoolThroughTheFitnessRanking();
}
```
