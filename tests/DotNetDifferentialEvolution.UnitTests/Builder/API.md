# API.md — UnitTests/Builder

Nothing outward. What this node proves about `DifferentialEvolutionBuilder` and the
variant extension point.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Value guards: bounds, population size, processors, local-search arguments, JADE's rate, L-SHADE's budget | `DifferentialEvolutionBuilderTests` | ✅ |
| `Build` refuses a population below the strategy's minimum and a mismatched L-SHADE budget | `DifferentialEvolutionBuilderTests` | ✅ |
| Archive capacities round half up (JADE, SHADE, L-SHADE) | `DifferentialEvolutionBuilderTests` | ✅ |
| A strategy that reads F and CR is refused without a provider, accepted with one; every built-in declares it | `MutationRequirementsValidationTests` | ✅ (see the BOOT ⚠ on which guard) |
| Each preset installs its mutation, its single provider-and-hook object, its tie rule and its archive | `DeVariantTests` | ✅ |
| A third-party variant is configured, validated and checked exactly like a built-in | `DeVariantTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Unit")]
public class DifferentialEvolutionBuilderTests
{
    public void WithBoundsThrowsWhenLengthsDiffer();
    public void WithBoundsThrowsWhenLowerExceedsUpper();
    public void WithPopulationSizeThrowsWhenNotPositive();
    public void UseProcessorsThrowsWhenNotPositive();
    public void WithJadeThrowsWhenArchiveSizeRateIsNegative();
    public void WithLShadeThrowsWhenEvaluationBudgetIsNotPositive();
    public void WithLShadeThrowsWhenTerminationEvaluationBudgetDoesNotMatch();
    public void WithLShadeBuildsWhenTerminationEvaluationBudgetMatches();
    public void BuildThrowsWhenPopulationIsTooSmallForTheMutationStrategy();
    public void BuildWithCompleteConfigurationProducesAUsableInstance();
    public void WithLocalSearchThrowsWhenRefinerIsNull();
    public void WithLocalSearchThrowsWhenIntervalIsNotPositive();
    public void WithJadeRoundsAMidpointArchiveCapacityHalfUp();
    public void WithShadeRoundsAMidpointArchiveCapacityHalfUp();
    public void WithLShadeRoundsAMidpointArchiveCapacityHalfUp();
}
[Trait("Category", "Unit")]
public class MutationRequirementsValidationTests
{
    public static TheoryData<IMutationStrategy> StrategiesNeedingControlParameters();
    public void BuildThrowsWhenAStrategyNeedingControlParametersHasNoProvider(IMutationStrategy mutationStrategy);
    public void BuildSucceedsWhenTheSameStrategyIsPairedWithAProvider(IMutationStrategy mutationStrategy);
    public void EveryStrategyNeedingControlParametersSaysSo();
    public void TheLegacyStrategyCarriesItsOwnParametersAndStillBuildsAlone();
    public void ACustomStrategyThatDeclaresNoRequirementsBuildsAlone();
    public void TheCurrentToPBestStrategyDeclaresTheRankingAndArchiveItReads();
    public void ThePresetVariantsSatisfyTheirOwnStrategysRequirements(string variant);
}
[Trait("Category", "Unit")]
public class DeVariantTests
{
    public void JdeInstallsRandOneWithASingleObjectAsProviderAndGenerationStrategy();
    public void JadeInstallsCurrentToPBestWithAnArchiveSizedFromThePopulation();
    public void EachPresetInstallsItsOwnPapersRuleForATie(string preset, SelectionOutcome expected);
    public void ShadeInstallsCurrentToPBestBackedByTheSuccessHistoryMemory();
    public void LShadeInstallsCurrentToPBestWithTheLargerArchiveItsPaperSpecifies();
    public void EveryPresetSatisfiesItsOwnMutationStrategysRequirements(string preset);
    public void AThirdPartyVariantIsConfiguredWithTheProblemDimensions();
    public void AThirdPartyVariantsValidateRunsAgainstTheCompletedConfiguration();
    public void AThirdPartyVariantCanRejectTheConfigurationFromItsOwnValidate();
    public void AThirdPartyVariantGetsTheSameControlParameterCheckAsABuiltIn();
    public void AThirdPartyVariantGetsTheSameMinimumPopulationCheckAsABuiltIn();
    public void AVariantThatChoosesNoSelectionStrategyGetsTheGreedyDefault();
    public void WithVariantRejectsNull();
}
```
