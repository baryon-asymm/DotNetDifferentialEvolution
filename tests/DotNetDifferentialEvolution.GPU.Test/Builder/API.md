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
```

Internal helpers: `Sphere`, `Throwing` (an objective with a `throw`), `IgnoringHandler`, and
`ForeignDevice`/`ForeignAccelerator` (an accelerator of type 99).
