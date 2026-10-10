# API.md — GPU.Test/Devices

Nothing outward. What this node proves about the GPU package's device selection, its CUDA
math through libdevice, and its math probe.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| D1, on every machine, no device opened (A12): with CUDA and OpenCL injected as absent, `Auto` gives `Cpu` and the reason "CUDA: no such device is present.; OpenCL: no such device is present." | `DeviceAbsenceTests.AutoFallsBackToTheCpuWithTheReasonsWhenNoGpuIsPresent` | ✅ |
| D1 and B1, the same: an explicit `Cuda` or `OpenCL` that is absent makes `Build` throw `InvalidOperationException` naming it and saying it was requested; never a fallback | `DeviceAbsenceTests.AnExplicitDeviceThatIsNotPresentFailsBuildNamingIt` | ✅ |
| The injected presence is asked in Auto's order, CUDA, OpenCL, CPU, and a denied backend is skipped | `DeviceAbsenceTests.TheInjectedPresenceIsAskedInAutosOrderUntilABackendOpens` | ✅ |
| D1 (`Gpu`): on this machine, `Auto` gives CUDA and no reason, or OpenCL and a reason naming CUDA, or the CPU with a reason | `DeviceSelectionTests.AutoTakesTheBestDeviceThisMachineHas` | ✅ local, owner's machine |
| B1 (`Gpu`): an explicit CUDA or OpenCL device is used with no reason where this machine has it, refused naming it where it does not | `DeviceSelectionTests.AnExplicitDeviceIsUsedWhereThisMachineHasItAndRefusedWhereItDoesNot` | ✅ local |
| D1 (`Gpu`): explicit `Cuda` is the RTX 5070 Ti, explicit `OpenCL` is `gfx1036`, no fallback reason | `AnExplicitDeviceIsTheOwnersGpu` | ✅ local, owner's machine only |
| D2 (`Gpu`): on CUDA, `Exp`, `Log`, `Pow(x, 1.37)`, `Sqrt` within 4 ULP of `System.Math` on 10⁴ arguments | `OnCudaTheFourFunctionsAreWithinFourUlpOfSystemMath` | ✅ local: 1, 1, 1, 0 ULP |
| The same probe on OpenCL runs; its distances are reported, not held to a tolerance | `OnOpenClTheProbeRunsAndItsDistancesAreReported` | ✅ informative |
| S15 / D3 (`Gpu`): on CUDA, `Cos` on [0, 2π] and `Tan` on [−π/2, π/2) within 4 ULP of `System.Math` on 10⁴ arguments each | `TrigProbeTests.OnCudaCosAndTanAreWithinFourUlpOfSystemMath` | ✅ local: 1 and 2 ULP |
| The trig probe on OpenCL runs; its distances are reported | `TrigProbeTests.OnOpenClTheTrigProbeRunsAndItsDistancesAreReported` | ✅ informative: 1 and 1 ULP |
| The ULP distance is right at neighbours, across an exponent boundary and across zero | `UlpTests` | ✅ |
| L1: libdevice discovery keeps its order on Windows and Linux, by parsed version, both layouts, bitcode required, each root once | `LibDeviceDiscoveryTests` | ✅ |
| L2: the wrapper inventory reads calls and definitions apart, on three PTX texts | `WrapperInventoryTests` | ✅ |
| L3: the post-link's definition check and its libnvvm and driver failures name what they must | `PostLinkGuardTests` | ✅ |
| L4: the ILGPU pin and the WSL reflection fail loudly, by name | `IlgpuPinTests` (first three facts) | ✅ |
| L5 (`Gpu`): on the RTX 5070 Ti the post-link compiles exactly the missing wrappers and the result loads | `CudaLibDeviceTests.OnTheRtx5070TiThePostLinkCompletesExactlyTheMissingWrappers` | ✅ local |
| L6, on every machine, no device opened (A12): with a CUDA device injected as present and no toolkit, explicit CUDA throws naming libnvvm and libdevice and Auto skips CUDA with that reason | `DeviceAbsenceTests.WithoutAToolkitCudaIsRefusedWithTheReason` | ✅ |
| L6, the same with the CUDA device injected as absent: the device, not the toolkit, is the reason | `DeviceAbsenceTests.WithoutACudaDeviceTheDeviceIsTheReason` | ✅ |
| L7 (`Gpu`): a bad libnvvm is named and costs no device memory | `CudaLibDeviceTests.ABadLibraryIsNamedAndNeverReachesTheDevice` | ✅ local |
| A1, on the CPU accelerator: two `KernelLoader.Load` calls for one kernel method give two `Kernel` objects; disposing the first leaves the second undisposed and launchable (the whole-run half is in [EndToEnd](../EndToEnd/API.md), `SharedKernelTests`) | `KernelLoaderTests.TwoLoadsOfOneMethodAreTwoKernelsDisposedIndependently` | ✅ |
| A2: `KernelLoader.GroupSize` gives, for warp 32, 70 multiprocessors and a limit of 640, 32 for extents 1 and 1 024, 256 for 16 384, 640 for 44 800 and 10⁶; for warp 64, 12 multiprocessors and a limit of 256, 128 for 1 024; it does not overflow at `int.MaxValue` and refuses an argument below 1 | `KernelLoaderTests` (the theories) | ✅ |
| L9: the WSL resolver failure is recognised by where it was thrown; (`Gpu`) three CUDA optimizers of one process each bind | `IlgpuPinTests.TheResolverFailureIsRecognisedByWhereItWasThrownNotByItsMessage`, `CudaLibDeviceTests.EveryCudaOptimizerOfTheProcessBinds` | ✅ on Windows; not run under WSL |

## Tests ✅

```csharp
public class DeviceAbsenceTests
{
    public DeviceAbsenceTests(ITestOutputHelper output);
    public void AutoFallsBackToTheCpuWithTheReasonsWhenNoGpuIsPresent();
    public void AnExplicitDeviceThatIsNotPresentFailsBuildNamingIt(GpuDevice device, string name);
    public void TheInjectedPresenceIsAskedInAutosOrderUntilABackendOpens();
    public void WithoutAToolkitCudaIsRefusedWithTheReason();
    public void WithoutACudaDeviceTheDeviceIsTheReason();
}
public class DeviceSelectionTests
{
    public DeviceSelectionTests(ITestOutputHelper output);
    [Trait("Category", "Gpu")]
    public void AutoTakesTheBestDeviceThisMachineHas();
    [Trait("Category", "Gpu")]
    public void AnExplicitDeviceIsUsedWhereThisMachineHasItAndRefusedWhereItDoesNot(GpuDevice device, string name);
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
public class TrigProbeTests
{
    public TrigProbeTests(ITestOutputHelper output);
    [Trait("Category", "Gpu")]
    public void OnCudaCosAndTanAreWithinFourUlpOfSystemMath();
    [Trait("Category", "Gpu")]
    public void OnOpenClTheTrigProbeRunsAndItsDistancesAreReported();
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
[Trait("Category", "Integration")]
public class KernelLoaderTests
{
    public void TheGroupSizeSpreadsTheExtentOverTheMultiprocessors(int extent, int warpSize, int multiprocessors, int occupancyLimit, int expected);
    public void TheLargestExtentDoesNotOverflow();
    public void AnArgumentBelowOneIsRefused(int extent, int warpSize, int multiprocessors, int occupancyLimit, string parameter);
    public void TwoLoadsOfOneMethodAreTwoKernelsDisposedIndependently();
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
    [Trait("Category", "Gpu")]
    public void ABadLibraryIsNamedAndNeverReachesTheDevice();
    [Trait("Category", "Gpu")]
    public void EveryCudaOptimizerOfTheProcessBinds();
}
```

Internal helpers: `KernelLoaderTests.Square` (the kernel the loads are of), `DevicePresence` (whether ILGPU sees a CUDA device, whether CUDA can be
opened with a toolkit, whether ILGPU sees an OpenCL device; it creates their contexts, so
only a `Gpu` test uses it), `DeviceSelectionTests.Stage()` (the builder at its device stage), `PtxFixtures` (the three PTX
texts of L2), `Ulp` (distance on the ordered bit patterns), `SumOfSquares`.
