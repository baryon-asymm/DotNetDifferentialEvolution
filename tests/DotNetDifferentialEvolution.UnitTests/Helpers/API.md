# API.md — UnitTests/Helpers

Nothing outward. What this node proves about the fitness ranking.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| `SortIndicesByFitness` orders ascending, best first, over the first `count` entries only | `OrdersIndicesAscendingByFitnessBestFirst`, `OnlyRanksTheFirstCountEntries` | ✅ |
| Ties keep a valid order with the strict minimum first | `PlacesTheMinimumFirstWhenValuesAreTied` | ✅ |
| `NaN` ranks last, never first; an all-`NaN` input still yields a permutation | the three `NaN` tests | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class PopulationSortHelperTests
{
    public void OrdersIndicesAscendingByFitnessBestFirst();
    public void OnlyRanksTheFirstCountEntries();
    public void PlacesTheMinimumFirstWhenValuesAreTied();
    public void RanksANaNIndividualLastRatherThanBest();
    public void RanksTheRealValuesCorrectlyWhenSeveralAreNaN();
    public void StillProducesAValidPermutationWhenEveryValueIsNaN();
}
```
