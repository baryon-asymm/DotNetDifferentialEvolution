# API.md — GPU.Test/Builder

Nothing outward. What this node proves about the GPU package's builder and `Build`: the
argument errors of the v1 contract, the compile error of an objective, and the initial
population `Build` samples.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| 1a: seeded, N = 1000, D = 3, box [−2, 5]: every gene is in [lower, upper) | `EveryGeneIsInTheBox` | ✅ |
| 1a: per gene, χ² on 20 bins is under the 0.999 quantile (df = 19, computed) | `EachGenePassesAChiSquaredTestOnTwentyBins` | ✅ |
| 1a: the evaluation count after `Build` is N | `BuildCostsNEvaluations` | ✅ |
| B1: bounds of different lengths, empty, lower > upper, non-finite → `ArgumentException` from `WithBounds` | the four bounds cases | ✅ |
| B1: N < 1 (⚠ 2026-10-05: was N < 4), N·D > `int.MaxValue` → `ArgumentOutOfRangeException` | the two population cases | ✅ |
| B1: F not finite or ≤ 0; CR outside [0, 1] → `ArgumentOutOfRangeException` | the F and CR cases | ✅ |
| B1: a limit < 1; `everyNGenerations` < 1 → `ArgumentOutOfRangeException` | the limit and period cases | ✅ |
| B1: `null` observer or accelerator → `ArgumentNullException` | `ANullObserverIsRejected`, `ANullAcceleratorIsRejected` | ✅ |
| An undefined `GpuDevice` → `ArgumentOutOfRangeException`; an accelerator of another type → `ArgumentException` | `AnUndefinedDeviceIsRejected`, `AnAcceleratorOfAnotherTypeIsRejected` | ✅ |
| B1: an objective ILGPU cannot compile → ILGPU's `InternalCompilerException` from `Build` | `AnObjectiveIlgpuCannotCompileFailsBuildWithIlgpusException` | ✅ |
| S1: the scheme stage has the CPU builder's nine methods by name, parameters, order and defaults, and no other | `TheSchemeStageHasTheCpuBuildersMethodsAndDefaults` | ✅ |
| B2: N below each scheme's minimum, an L-SHADE limit other than its budget, an archive beyond the index range → `InvalidOperationException` from `Build`; p, the archive rate, c, H, the budget, jDE's initial F and CR and the stagnation arguments out of range → `ArgumentOutOfRangeException`; each minimum and edge passes | `SymmetryBuilderTests` | ✅ |
| Each guard is no wider than the contract (N = 4, the edges of F and CR, limits and period of 1, equal bounds pass) | the boundary cases | ✅ |
| A5: `ForPointwiseFunction` accepts `double`, `int`, an `int` enum, a struct of a `double` and an `int` (P1's point type), of two `double`s and an `int` (P3's and P4's shape), a struct nesting two such, a struct with an `int` enum, one with a `long` enum and a `float`, and one of `byte`, `short`, `byte` padded to 6 bytes | `PointTypeTests`, the nine accepted cases | ✅ |
| A5: it throws `ArgumentException` (`ParamName` `TPoint`; the message names the type and the field, nested ones by path, or the sizes) for a `bool` field, a `char` field, an `nint` field, a `bool` itself, `Pack = 1 {byte; double}`, `Pack = 4 {int; double}`, `Size = 24` on a 16-byte pair, `LayoutKind.Auto`, `LayoutKind.Explicit`, a struct nesting a `bool` struct (`Inner.Deep.Flag`) and one nesting a packed one (`Inner.Value`) | `PointTypeTests`, the eleven refused cases | ✅ |
| A9: `WithPopulationSize` refuses N above `int.MaxValue − 1 023` and, for a pointwise objective, N·P above it (N = 4, P = 536 870 911; one above the limit), with `ArgumentOutOfRangeException` naming the product and the limit; N and N·P at the limit (2³¹ − 2¹⁰) pass | `APopulationAboveTheLastGroupsIndexLimitIsRejected`, `FourIndividualsOfFiveHundredThirtySixMillionPointsAreRejected`, `APointwisePopulationIsRejectedOneAboveTheLimitAndAcceptedAtIt` | ✅ |
| A9: `Build` checks N·D again: a stage retained across a second, longer `WithBounds` (2⁵⁰ − 2²⁰ genes) → `InvalidOperationException` naming the product and `int.MaxValue`, before a device is opened; at the edge N = 2³⁰ and D = 2 (2³¹) is refused and N = 2³⁰ − 1 (2³¹ − 2) passes; a longer bound within the range builds. N·P is checked there too but cannot be reached: P is fixed at the first call and N is checked when set | `ARetainedStageWithLongerBoundsIsRefusedByBuild`, `ARetainedStageIsRefusedOneAboveTheGenesIndexRangeAndAcceptedBelowIt`, `ARetainedStageWithinTheRangeBuilds` | ✅ |
| A9: JADE, SHADE and L-SHADE refuse N = 2³⁰ + 1 at `Build` with `InvalidOperationException` naming the scheme and 2³⁰; N = 2³⁰ passes the configuration check; rand/1 and jDE pass it at 2³⁰ + 1 | `ARankingSchemeRefusesAPopulationAboveTwoToTheThirtyAtBuild`, `ARankingSchemeAcceptsAPopulationOfTwoToTheThirty`, `TheSchemesThatDoNotRankAcceptAPopulationAboveTwoToTheThirty` | ✅ |

## Tests ✅

```csharp
public class InitialSamplingTests
{
    public InitialSamplingTests(ITestOutputHelper output);
    public void EveryGeneIsInTheBox();
    public void EachGenePassesAChiSquaredTestOnTwentyBins();
    public void BuildCostsNEvaluations();
}
public class BuilderErrorTests
{
    public void BoundsOfDifferentLengthsAreRejected();
    public void EmptyBoundsAreRejected();
    public void ALowerBoundAboveItsUpperBoundIsRejected();
    public void EqualBoundsAreAccepted();
    public void NonFiniteBoundsAreRejected(double lower, double upper);
    public void APopulationOfFewerThanOneIsRejected(int populationSize);
    public void APopulationOfFourIsAccepted();
    public void APopulationBeyondTheKernelsIndexRangeIsRejected();
    public void AMutationForceNotFiniteOrNotPositiveIsRejected(double mutationForce);
    public void ACrossoverProbabilityOutsideTheUnitIntervalIsRejected(double crossoverProbability);
    public void TheEdgesOfFAndCrAreAccepted(double mutationForce, double crossoverProbability);
    public void AGenerationLimitBelowOneIsRejected(int maxGenerations);
    public void AnEvaluationLimitBelowOneIsRejected(long maxEvaluations);
    public void AnObserverPeriodBelowOneIsRejected(int everyNGenerations);
    public void ANullObserverIsRejected();
    public void ANullAcceleratorIsRejected();
    public void AnUndefinedDeviceIsRejected(int device);
    public void AnAcceleratorOfAnotherTypeIsRejected();
    public void AnObjectiveIlgpuCannotCompileFailsBuildWithIlgpusException();
}
public class SymmetryBuilderTests
{
    public static TheoryData<string, int> Minimums();
    public void TheSchemeStageHasTheCpuBuildersMethodsAndDefaults();
    public void APopulationBelowTheSchemesMinimumIsRefusedByBuild(string method, int minimum);
    public void APBestRateOutsideZeroToOneIsRejected(double pBestRate);
    public void AnArchiveSizeRateThatIsNegativeOrNotFiniteIsRejected(double archiveSizeRate);
    public void AnAdaptationRateOutsideZeroToOneIsRejected(double adaptationRate);
    public void AMemorySizeBelowOneIsRejected();
    public void AnLShadeBudgetBelowOneIsRejected();
    public void JdesInitialParametersOutsideTheirRangesAreRejected(double initialMutationForce, double initialCrossoverProbability);
    public void AStagnationLimitOutsideItsRangesIsRejected(int maxStagnationStreak, double stagnationThreshold);
    public void LShadeWithAnotherEvaluationLimitIsRefusedByBuild();
    public void AnArchiveBeyondTheIndexRangeIsRefusedByBuild();
}
public class PointTypeTests
{
    // accepted
    public void ADoubleIsAccepted();
    public void AnIntIsAccepted();
    public void AnIntEnumIsAccepted();
    public void ADoubleAndAnIntAreAccepted();
    public void TwoDoublesAndAnIntAreAccepted();
    public void AStructNestingTwoAcceptedStructsIsAccepted();
    public void AStructWithAnIntEnumIsAccepted();
    public void AStructWithALongEnumAndAFloatIsAccepted();
    public void AStructOfSmallFieldsWithNaturalPaddingIsAccepted();
    // refused
    public void ABoolFieldIsRefused();
    public void ACharFieldIsRefused();
    public void ANativeIntFieldIsRefused();
    public void ABoolIsRefused();
    public void APackOfOneByteAndDoubleIsRefused();
    public void APackOfFourIntAndDoubleIsRefused();
    public void AStructPaddedBySizeIsRefused();
    public void AutoLayoutIsRefused();
    public void ExplicitLayoutIsRefused();
    public void AStructNestingARefusedOneIsRefusedByThePathToTheField();
    public void AStructNestingAPackedOneIsRefusedByThePathToTheField();
}
public class PopulationLimitTests
{
    public static TheoryData<string> RankingSchemes();
    public void APopulationAboveTheLastGroupsIndexLimitIsRejected();
    public void FourIndividualsOfFiveHundredThirtySixMillionPointsAreRejected();
    public void APointwisePopulationIsRejectedOneAboveTheLimitAndAcceptedAtIt();
    public void ARetainedStageWithLongerBoundsIsRefusedByBuild();
    public void ARetainedStageIsRefusedOneAboveTheGenesIndexRangeAndAcceptedBelowIt();
    public void ARetainedStageWithinTheRangeBuilds();
    public void ARankingSchemeRefusesAPopulationAboveTwoToTheThirtyAtBuild(string method);
    public void ARankingSchemeAcceptsAPopulationOfTwoToTheThirty(string method);
    public void TheSchemesThatDoNotRankAcceptAPopulationAboveTwoToTheThirty();
}
```

Internal helpers: `Sphere`, `Throwing` (an objective with a `throw`), `IgnoringHandler`, and
`ForeignDevice`/`ForeignAccelerator` (an accelerator of type 99). For A5, `PointProbe<TPoint>`
(a pointwise objective of any point type, never run) and the point types `DoubleAndInt`,
`TwoDoublesAndInt`, `NestingTwo`, `WithIntEnum`, `WithWideEnum`, `SmallFields`, `WithBool`,
`WithChar`, `WithNativeInt`, `PackedByteDouble`, `PackedIntDouble`, `SizedPair`, `AutoPair`,
`ExplicitPair`, `NestingBool`, `NestingPacked` and the enums `Verdict`, `WideVerdict`.

**The seam of A9.** `GpuBuilder<TFunction>.ValidateConfiguration()` (internal to the GPU
assembly) runs what `Build` runs before it opens a device, so that the accepted edges of
populations too large to allocate (N = 2³⁰, N·D = 2³¹ − 2) are checked without allocating.
`Build` calls it first; the refusals are also asserted through `Build` itself.
