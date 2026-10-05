# API.md — UnitTests/Algorithms

Nothing outward. What this node proves about the four adaptive strategies.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| jDE returns the stored pair without regeneration, regenerates F in range and CR uniformly when triggered, and inherits parameters on survival (ties included) | `JdeStrategyTests` | ✅ |
| JADE moves μCR to the arithmetic and μF to the Lehmer mean of improving trials only; a tie moves nothing and archives nothing | `JadeStrategyTests` | ✅ |
| SHADE writes improvement-weighted means, skips non-finite weights, and keeps the terminal slot rule switchable | `ShadeStrategyTests` | ✅ |
| L-SHADE reduces linearly, rounds half up, keeps the best in order, takes the Lehmer `M_CR` with the terminal rule first, refuses silent-failure arguments | `LShadeStrategyTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class JdeStrategyTests
{
    public void WithoutAdaptationReturnsTheStoredPerIndividualParameters();
    public void WhenAdaptationTriggersRegeneratesFWithinRangeAndCrUniformly();
    public void AfterGenerationKeepsParametersOfSuccessfulTrialsPerIndividual();
    public void AfterGenerationKeepsParametersOfATrialAcceptedOnATie();
}
[Trait("Category", "Unit")]
public class JadeStrategyTests
{
    public void AfterGenerationNudgesMeansTowardSuccessfulParameters();
    public void AfterGenerationWithNoSuccessesLeavesMeansUnchanged();
    public void AfterGenerationIgnoresATrialAcceptedOnATie();
    public void AfterGenerationWithANegativeArchiveCapacityLeavesTheArchiveAlone();
}
[Trait("Category", "Unit")]
public class ShadeStrategyTests
{
    public void ConstructorThrowsWhenMemorySizeIsNotPositive();
    public void AfterGenerationStoresImprovementWeightedMeans();
    public void AfterGenerationWithNoSuccessesLeavesMemoryUnchanged();
    public void AfterGenerationWithTerminalCrEnabledFixesSlotToZeroWhenAllSuccessfulCrAreZero();
    public void AfterGenerationTerminalCrSlotStaysTerminalEvenAfterNonZeroSuccessfulCr();
    public void AfterGenerationWithTerminalCrDisabledKeepsZeroMeanAsAnOrdinaryValue();
    public void AfterGenerationIgnoresASuccessWhoseImprovementIsNotMeasurable();
    public void AfterGenerationIgnoresASuccessOverAnInfiniteParent();
}
[Trait("Category", "Unit")]
public class LShadeStrategyTests
{
    public void AfterGenerationReducesPopulationLinearlyWithTheEvaluationBudget(long evaluationCount, int expectedPopulationSize);
    public void AfterGenerationKeepsTheBestSurvivorsInAscendingFitnessOrder();
    public void AfterGenerationRoundsMidpointPopulationSizesHalfUp(int initialPopulationSize, long maxEvaluationNumber, long evaluationCount, int expectedPopulationSize);
    public void AfterGenerationRoundsAMidpointArchiveCapacityHalfUp();
    public void AfterGenerationUpdatesMemoryCrWithTheWeightedLehmerMean();
    public void AfterGenerationTerminalCrRuleWinsOverTheLehmerMean();
    public void ConstructorRejectsANonPositiveEvaluationBudget(long maxEvaluationNumber);
    public void ConstructorRejectsANegativeArchiveSizeRate();
    public void ConstructorValidatesMinimumPopulationSize(int minPopulationSize);
}
```
