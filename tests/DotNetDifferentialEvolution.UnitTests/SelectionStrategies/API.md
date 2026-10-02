# API.md — UnitTests/SelectionStrategies

Nothing outward. What this node proves about greedy selection.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| A strictly better trial replaces the parent and is reported `TrialImproved`; a worse one is `ParentKept` | `AcceptsTrialWhenStrictlyBetter`, `KeepsParentWhenTrialIsWorse` | ✅ |
| An equal trial survives as `TrialAccepted`, or is refused when ties are off | the tie tests | ✅ |
| A `NaN` parent loses to any real trial (as an improvement, ties on or off); a `NaN` trial loses; two `NaN`s are not a tie | the four `NaN` tests | ✅ |
| Genes and fitness land at the individual's offset in the next buffers | every case | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class SelectionStrategyTests
{
    public void AcceptsTrialWhenStrictlyBetter();
    public void KeepsParentWhenTrialIsWorse();
    public void TakesTheTrialWhenFitnessIsEqualButDoesNotCallItAnImprovement();
    public void WithTiesRejectedKeepsTheParentOnEqualFitness();
    public void WithTiesRejectedStillTakesAStrictlyBetterTrial();
    public void WithTiesRejectedAParentScoredNaNIsStillReplaced();
    public void AcceptsTrialWhenParentFitnessIsNaN();
    public void KeepsParentWhenTrialFitnessIsNaN();
    public void KeepsParentWhenBothFitnessValuesAreNaN();
}
```
