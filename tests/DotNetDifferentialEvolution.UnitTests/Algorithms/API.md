# API.md — UnitTests/Algorithms

Nothing outward. What this node proves about the four adaptive strategies.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| jDE returns the stored pair without regeneration, regenerates F in range and CR uniformly when triggered, and inherits parameters on survival (ties included) | `JdeStrategyTests` | ✅ |
| JADE moves μCR to the arithmetic and μF to the Lehmer mean of improving trials only; a tie moves nothing and archives nothing | `JadeStrategyTests` | ✅ |
| SHADE writes improvement-weighted means, skips non-finite weights, keeps the terminal slot rule switchable, and keeps the memory finite when finite improvements overflow the sums (parents scored `double.MaxValue`) | `ShadeStrategyTests` | ✅ |
| L-SHADE reduces linearly, rounds half up, keeps the best in order, takes the Lehmer `M_CR` with the terminal rule first, refuses silent-failure arguments | `LShadeStrategyTests` | ✅ |
| Below the overflow bound the memory of SHADE (200 sets, seed 20261008) and of L-SHADE (200 sets, seed 20261009, terminal slots included) equals 6.0.0's unscaled arithmetic bit for bit | `…BelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit`, against `UnscaledShadeMemory` | ✅ |

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
    public void AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums();
    public void AfterGenerationWeighsAnImprovementNearTheLargestDoubleAgainstOneOfOne();
    public void AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit();
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
    public void AfterGenerationKeepsTheMemoryFiniteWhenTheImprovementsOverflowTheSums();
    public void AfterGenerationWeighsAnImprovementNearTheLargestDoubleAgainstOneOfOne();
    public void AfterGenerationBelowTheOverflowBoundEqualsTheUnscaledArithmeticBitForBit();
    public void ConstructorRejectsANonPositiveEvaluationBudget(long maxEvaluationNumber);
    public void ConstructorRejectsANegativeArchiveSizeRate();
    public void ConstructorValidatesMinimumPopulationSize(int minPopulationSize);
}
```

## Reference for O2 ✅

```csharp
internal sealed class UnscaledShadeMemory
{
    public UnscaledShadeMemory(int memorySize, double initialMemoryValue,
        bool useTerminalCr, bool useLehmerCrMean);
    public void Update(ReadOnlySpan<TrialRecord> trialRecords, int currentPopulationSize);
    public bool IsTerminal(int slot);
    public double ReadableF(int slot);
    public double ReadableCr(int slot);
    public static void ReadBack(ShadeStrategy strategy, int slot, bool terminal,
        out double f, out double cr);
    public static void AssertTheStrategyEqualsTheUnscaledArithmetic(int seed,
        bool useTerminalCr, bool useLehmerCrMean, bool allZeroCrInTheFirstSet,
        Func<int, int, ShadeStrategy> createStrategy, Func<int, ProblemContext> createContext);
}
```

`Update` is 6.0.0's `UpdateMemory` as at `a5e579e`, copied once: weights summed as they
are. `ReadBack` reads a slot exactly through `GetControlParameters` (scripted slot, the
Gaussian's uniforms 0, the Cauchy's ½; a terminal slot has no Gaussian draw), and
`ReadableF`/`ReadableCr` give the value that read must return (F capped at 1, CR clamped,
0 when terminal). The comparison runs 200 random sets of two generations (N 4 to 8, H 1 to 3:
tiny to 10³⁰⁵ improvements, NaN and infinite parents, ties, kept trials; with
`allZeroCrInTheFirstSet`, set 0 is four improvements whose CR are all 0) and compares the bit
patterns of F and CR of every slot.
