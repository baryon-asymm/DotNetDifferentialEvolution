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
| A15: the rule on known answers — visible: a public struct, a public type nested in a public class, an internal struct and an internal nested one in an assembly that grants `ILGPURuntime` (case-insensitively, among other grants, in a dynamic assembly too), a protected internal struct there, a public struct in an internal class there, a public generic over a public struct, an open generic; not visible: an internal struct without the grant (or with a grant to another assembly, or to a longer name), a public struct in an internal class without it, a private, a protected and a private protected nested struct (the grant notwithstanding), a public struct in a private class, a public generic over a private struct or over an ungranted internal one, a generic over a generic over a private one, an array of a private one; the part named is the first of the type, its enclosing types, then its generic arguments | `ObjectiveVisibilityTests`, 22 cases | ✅ |
| A15: a private nested objective, which ILGPU's CPU accelerator refuses with an `InternalCompilerException` holding a `TypeLoadException` (measured 2026-10-11), makes `Build` throw `InvalidOperationException` naming the type (`FullName`) and both remedies, ILGPU's exception as `InnerException`; a pointwise one names the objective | `APrivateNestedObjectiveIsRefusedWithTheTypeAndTheRemedies`, `APrivateNestedPointwiseObjectiveIsRefusedNamingTheObjective` | ✅ |
| A15: through the seam, a load that throws `TypeLoadException` for an invisible type becomes the same exception with that `TypeLoadException` as `InnerException` (also one beneath another exception, and a type invisible by its generic argument, which is named with the argument); an invisible point type is named when the objective is visible; the release failures are in the hint's `Data["DotNetDifferentialEvolution.GPU.ReleaseFailures"]` | `ATypeLoadFailureOnAnInvisibleObjectiveBecomesTheHint`, `ATypeLoadFailureBeneathAnotherExceptionOnAnInvisibleObjectiveBecomesTheHint`, `AnObjectiveInvisibleByAGenericArgumentNamesTheArgument`, `AnInvisiblePointTypeIsNamedWhenTheObjectiveIsVisible`, `TheReleaseFailuresRideOnTheHint` | ✅ |
| A15: a visible type's `TypeLoadException` (objective or point type), and any other exception for an invisible type, propagate as they are (the same instance) | `ATypeLoadFailureOnAVisibleObjectiveIsNotWrapped`, `ATypeLoadFailureOnAVisiblePointTypeIsNotWrapped`, `AnotherFailureOnAnInvisibleObjectiveIsNotWrapped` | ✅ |
| A15, **Gpu**: on CUDA, a private nested objective makes `Build` throw the hint | `APrivateNestedObjectiveIsRefusedOnCuda` (run by the orchestrator) | ⏳ |

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
public class ObjectiveVisibilityTests
{
    // visible
    public void APublicStructIsVisible();
    public void APublicTypeNestedInAPublicClassIsVisible();
    public void AnInternalStructWithTheAttributeIsVisible();
    public void AnInternalNestedStructWithTheAttributeIsVisible();
    public void AProtectedInternalStructWithTheAttributeIsVisible();
    public void AnInternalStructOfADynamicAssemblyWithTheAttributeIsVisible();
    public void TheGrantIsMatchedWithoutRegardToCase();
    public void AGrantAmongOthersMakesAnInternalStructVisible();
    public void APublicStructInAnInternalClassWithTheAttributeIsVisible();
    public void APublicGenericOverAPublicStructIsVisible();
    public void AnOpenPublicGenericIsVisible();
    // not visible
    public void AnInternalStructWithoutTheAttributeIsNotVisible();
    public void AGrantToAnotherAssemblyDoesNotMakeAnInternalStructVisible();
    public void APublicStructInAnInternalClassWithoutTheAttributeIsNotVisible();
    public void APrivateNestedStructIsNotVisible();
    public void AProtectedNestedStructIsNotVisible();
    public void APrivateProtectedNestedStructIsNotVisible();
    public void APublicStructInAPrivateClassIsNotVisible();
    public void APublicGenericOverAPrivateStructIsNotVisible();
    public void APublicGenericOverAnInternalStructWithoutTheAttributeIsNotVisible();
    public void AGenericOverAGenericOverAPrivateStructIsNotVisible();
    public void AnArrayOfAPrivateStructIsNotVisible();
}
public class InvisibleObjectiveTests
{
    public void APrivateNestedObjectiveIsRefusedWithTheTypeAndTheRemedies();
    public void APrivateNestedPointwiseObjectiveIsRefusedNamingTheObjective();
    public void ATypeLoadFailureOnAnInvisibleObjectiveBecomesTheHint();
    public void AnObjectiveInvisibleByAGenericArgumentNamesTheArgument();
    public void ATypeLoadFailureOnAVisibleObjectiveIsNotWrapped();
    public void AnotherFailureOnAnInvisibleObjectiveIsNotWrapped();
    public void ATypeLoadFailureBeneathAnotherExceptionOnAnInvisibleObjectiveBecomesTheHint();
    public void TheReleaseFailuresRideOnTheHint();
    public void AnInvisiblePointTypeIsNamedWhenTheObjectiveIsVisible();
    public void ATypeLoadFailureOnAVisiblePointTypeIsNotWrapped();
    [Trait("Category", "Gpu")] public void APrivateNestedObjectiveIsRefusedOnCuda();
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

**The seam of A15.** `GpuBuilder<TFunction>.WithLauncherFactory(Func<Accelerator, TFunction,
ParameterRule, int, KernelLauncher>)` (internal to the GPU assembly), beside `WithPlantedRelease`
and `WithDevicePresence`, replaces what `Build` calls to compile the kernels for the objective, so
that a load that throws `TypeLoadException` (or anything else) can be planted on the CPU
accelerator. The pointwise path is reached by the builder's own constructor, whose fourth
argument is `TPoint` (`new GpuBuilder<Sphere>(default, 3, factory, pointType)`). Internal helpers
of A15: `TypesOfEveryAccessibility` (an internal class whose nested structs are of every
accessibility, fetched by name), `DynamicTypes` (types of dynamic assemblies that grant, or do
not grant, `ILGPURuntime` access), `Wrapping<T>` and the private nested objectives of
`InvisibleObjectiveTests`.
