# API.md — UnitTests/TerminationStrategies

Nothing outward. What this node proves about the three stop rules.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| The generation and evaluation limits stop exactly at the limit (`>=`), not before, and stay stopped beyond it | the two limit tests | ✅ |
| The stagnation rule counts generations without an improvement above the threshold, resets on one, and does not move its baseline on a smaller change | `StagnationStreakTerminationStrategyTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class LimitEvaluationNumberTerminationStrategyTests
{
    public void TerminatesOnceTheEvaluationBudgetIsReached(long evaluationCount, long maxEvaluationNumber, bool expected);
}
[Trait("Category", "Unit")]
public class LimitGenerationNumberTerminationStrategyTests
{
    public void TerminatesOnceTheGenerationLimitIsReached(int generationNumber, int maxGenerationNumber, bool expected);
}
[Trait("Category", "Unit")]
public class StagnationStreakTerminationStrategyTests
{
    public void AccumulatesStreakAndTerminatesAfterMaxStagnantGenerations();
    public void ImprovementGreaterThanThresholdResetsTheStreak();
    public void ImprovementSmallerThanThresholdDoesNotResetTheStreak();
}
```
