# API.md — GPU.Test/Devices

Nothing outward. What this node proves about the GPU package's device selection, its CUDA
math through libdevice, and its math probe.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| D1: with no GPU, `Auto` gives `Cpu` with a non-`null` `FallbackReason`; with CUDA, `Auto` gives CUDA and no reason; with OpenCL only, OpenCL and a reason naming CUDA | `AutoFallsBackToTheCpuWithAReasonWhenNoGpuIsPresent` | ✅ all three branches run locally, the GPUs hidden by environment |
| D1: with no CUDA device, an explicit `Cuda` makes `Build` throw `InvalidOperationException` naming CUDA | `AnExplicitCudaWithoutACudaDeviceThrowsNamingCuda` | ✅ both branches run locally |
| B1: an explicit CUDA or OpenCL device that is not present → `InvalidOperationException` from `Build`, naming it; never a fallback | `AnExplicitDeviceThatIsNotPresentFailsBuildNamingIt` | ✅ both branches run locally |
| D1 (`Gpu`): explicit `Cuda` is the RTX 5070 Ti, explicit `OpenCL` is `gfx1036`, no fallback reason | `AnExplicitDeviceIsTheOwnersGpu` | ✅ local, owner's machine only |
| D2 (`Gpu`): on CUDA, `Exp`, `Log`, `Pow(x, 1.37)`, `Sqrt` within 4 ULP of `System.Math` on 10⁴ arguments | `OnCudaTheFourFunctionsAreWithinFourUlpOfSystemMath` | ✅ local: 1, 1, 1, 0 ULP |
| The same probe on OpenCL runs; its distances are reported, not held to a tolerance | `OnOpenClTheProbeRunsAndItsDistancesAreReported` | ✅ informative |
| The ULP distance is right at neighbours, across an exponent boundary and across zero | `UlpTests` | ✅ |
| L1: libdevice discovery keeps its order on Windows and Linux, by parsed version, both layouts, bitcode required, each root once | `LibDeviceDiscoveryTests` | ✅ |
| L2: the wrapper inventory reads calls and definitions apart, on three PTX texts | `WrapperInventoryTests` | ✅ |
| L3: the post-link's definition check and its libnvvm and driver failures name what they must | `PostLinkGuardTests` | ✅ |
| L4: the ILGPU pin and the WSL reflection fail loudly, by name | `IlgpuPinTests` (first three facts) | ✅ |
| L5 (`Gpu`): on the RTX 5070 Ti the post-link compiles exactly the missing wrappers and the result loads | `CudaLibDeviceTests.OnTheRtx5070TiThePostLinkCompletesExactlyTheMissingWrappers` | ✅ local |
| L6: without a toolkit, explicit CUDA throws naming libnvvm and libdevice, Auto skips CUDA with that reason; without a CUDA device, the device is the reason | `CudaLibDeviceTests.WithoutAToolkitCudaIsRefusedWithTheReason` | ✅ both branches run locally, the GPUs hidden by environment |
| L7 (`Gpu`): a bad libnvvm is named and costs no device memory | `CudaLibDeviceTests.ABadLibraryIsNamedAndNeverReachesTheDevice` | ✅ local |
| L9: the WSL resolver failure is recognised by where it was thrown; (`Gpu`) three CUDA optimizers of one process each bind | `IlgpuPinTests.TheResolverFailureIsRecognisedByWhereItWasThrownNotByItsMessage`, `CudaLibDeviceTests.EveryCudaOptimizerOfTheProcessBinds` | ✅ on Windows; not run under WSL |

## Tests ✅

```csharp
public class DeviceSelectionTests
{
    public DeviceSelectionTests(ITestOutputHelper output);
    public void AutoFallsBackToTheCpuWithAReasonWhenNoGpuIsPresent();
    public void AnExplicitCudaWithoutACudaDeviceThrowsNamingCuda();
    public void AnExplicitDeviceThatIsNotPresentFailsBuildNamingIt(GpuDevice device, string name);
    [Trait("Category", "Gpu")]
    public void AnExplicitDeviceIsTheOwnersGpu(GpuDevice device, string expectedName);
}
public class MathProbeTests
{
    public MathProbeTests(ITestOutputHelper output);
    [Trait("Category", "Gpu")]
    public void OnCudaTheFourFunctionsAreWithinFourUlpOfSystemMath();
    [Trait("Category", "Gpu")]
    public void OnOpenClTheProbeRunsAndItsDistancesAreReported();
}
public class UlpTests
{
    public void EqualValuesAreZeroApart();
    public void NeighboursAreOneApart(double value);
    public void StepsAddUpAcrossAnExponentBoundary();
    public void TheCountIsRightAcrossZero();
    public void NaNIsAsFarAsItGets();
}
public sealed class LibDeviceDiscoveryTests : IDisposable
{
    public void Dispose();
    public void AnUnsupportedPlatformTriesNothing();
    public void WindowsWithNoCudaPathAndNoBaseDirectoryTriesNothing();
    public void WindowsTriesCudaPathBeforeTheVersionedDirectories();
    public void WindowsOrdersTheVersionedDirectoriesNewestFirst();
    public void WindowsFindsBothLibraryLayouts(bool legacyLayout);
    public void ALibraryWithoutBitcodeIsPassedOverForTheNextRoot();
    public void WindowsTriesARootNamedTwiceOnce();
    public void LinuxTriesCudaPathFirst();
    public void LinuxTriesCudaHomeThenTheFixedRootThenTheVersions();
    public void LinuxOrdersTheVersionedDirectoriesNewestFirst();
    public void LinuxTriesARootNamedTwiceOnce();
}
public class WrapperInventoryTests
{
    public void IlgpusOwnPtxForSm120CallsWrappersAndDefinesNone();
    public void TheLinkedPtxDefinesEveryWrapperItCalls();
    public void BelowCompute10IlgpuDefinesEveryWrapperItCalls();
    public void AllThreeTextsCallTheSameWrappers();
    public void NoParameterNameIsReadAsACall();
    public void TheInventoryIsTheSameWithLfAndCrlf();
}
public class PostLinkGuardTests
{
    public void EveryWrapperWithADefinitionPasses();
    public void AMissingDefinitionIsNamedAndOnlyIt();
    public void ACallSiteIsNotADefinition();
    public void SuccessThrowsNothing();
    public void EveryNvvmFailureNamesTheLibraryTheCallTheResultAndTheTarget(NvvmResult result);
    public void EveryDriverFailureNamesTheLibraryTheCallTheResultAndTheTarget(CudaError error);
    public void ALogIsCarriedWithoutItsPadding();
    public void ALogThatTrimsToNothingIsNoLog();
    public static TheoryData<NvvmResult> NonSuccessNvvmResults();
    public static TheoryData<CudaError> NonSuccessCudaErrors();
}
public class IlgpuPinTests
{
    public void TheReferencedIlgpuPassesTheAssertion();
    public void AnotherExpectedVersionThrowsNamingBoth();
    public void TheWslReflectionNamesAMissingMember();
    public void TheResolverFailureIsRecognisedByWhereItWasThrownNotByItsMessage();
}
public class CudaLibDeviceTests
{
    public CudaLibDeviceTests(ITestOutputHelper output);
    [Trait("Category", "Gpu")]
    public void OnTheRtx5070TiThePostLinkCompletesExactlyTheMissingWrappers();
    public void WithoutAToolkitCudaIsRefusedWithTheReason();
    [Trait("Category", "Gpu")]
    public void ABadLibraryIsNamedAndNeverReachesTheDevice();
    [Trait("Category", "Gpu")]
    public void EveryCudaOptimizerOfTheProcessBinds();
}
```

Internal helpers: `DevicePresence` (whether ILGPU sees a CUDA device, whether CUDA can be
opened with a toolkit, whether ILGPU sees an OpenCL device), `PtxFixtures` (the three PTX
texts of L2), `Ulp` (distance on the ordered bit patterns), `SumOfSquares`.
