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
    public void WithoutAdaptation_ReturnsTheStoredPerIndividualParameters();
    public void WhenAdaptationTriggers_RegeneratesFWithinRangeAndCrUniformly();
    public void AfterGeneration_KeepsParametersOfSuccessfulTrialsPerIndividual();
    public void AfterGeneration_KeepsParametersOfATrialAcceptedOnATie();
}
[Trait("Category", "Unit")]
public class JadeStrategyTests
{
    public void AfterGeneration_NudgesMeansTowardSuccessfulParameters();
    public void AfterGeneration_WithNoSuccesses_LeavesMeansUnchanged();
    public void AfterGeneration_IgnoresATrialAcceptedOnATie();
    public void AfterGeneration_WithANegativeArchiveCapacity_LeavesTheArchiveAlone();
}
[Trait("Category", "Unit")]
public class ShadeStrategyTests
{
    public void Constructor_ThrowsWhenMemorySizeIsNotPositive();
    public void AfterGeneration_StoresImprovementWeightedMeans();
    public void AfterGeneration_WithNoSuccesses_LeavesMemoryUnchanged();
    public void AfterGeneration_WithTerminalCrEnabled_FixesSlotToZeroWhenAllSuccessfulCrAreZero();
    public void AfterGeneration_TerminalCrSlotStaysTerminal_EvenAfterNonZeroSuccessfulCr();
    public void AfterGeneration_WithTerminalCrDisabled_KeepsZeroMeanAsAnOrdinaryValue();
    public void AfterGeneration_IgnoresASuccessWhoseImprovementIsNotMeasurable();
    public void AfterGeneration_IgnoresASuccessOverAnInfiniteParent();
}
[Trait("Category", "Unit")]
public class LShadeStrategyTests
{
    public void AfterGeneration_ReducesPopulationLinearlyWithTheEvaluationBudget(long evaluationCount, int expectedPopulationSize);
    public void AfterGeneration_KeepsTheBestSurvivorsInAscendingFitnessOrder();
    public void AfterGeneration_RoundsMidpointPopulationSizesHalfUp(int initialPopulationSize, long maxEvaluationNumber, long evaluationCount, int expectedPopulationSize);
    public void AfterGeneration_RoundsAMidpointArchiveCapacityHalfUp();
    public void AfterGeneration_UpdatesMemoryCrWithTheWeightedLehmerMean();
    public void AfterGeneration_TerminalCrRuleWinsOverTheLehmerMean();
    public void Constructor_RejectsANonPositiveEvaluationBudget(long maxEvaluationNumber);
    public void Constructor_RejectsANegativeArchiveSizeRate();
    public void Constructor_ValidatesMinimumPopulationSize(int minPopulationSize);
}
```
