# API.md — GPU.Test/Devices

Nothing outward. What this node proves about the GPU package's device selection and its
CUDA math probe.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| D1: with no GPU, `Auto` gives `Cpu` with a non-`null` `FallbackReason`; with CUDA, `Auto` gives CUDA and no reason; with OpenCL only, OpenCL and a reason naming CUDA | `AutoFallsBackToTheCpuWithAReasonWhenNoGpuIsPresent` | ✅ all three branches run locally, the GPUs hidden by environment |
| D1: with no CUDA device, an explicit `Cuda` makes `Build` throw `InvalidOperationException` naming CUDA | `AnExplicitCudaWithoutACudaDeviceThrowsNamingCuda` | ✅ both branches run locally |
| B1: an explicit CUDA or OpenCL device that is not present → `InvalidOperationException` from `Build`, naming it; never a fallback | `AnExplicitDeviceThatIsNotPresentFailsBuildNamingIt` | ✅ both branches run locally |
| D1 (`Gpu`): explicit `Cuda` is the RTX 5070 Ti, explicit `OpenCL` is `gfx1036`, no fallback reason | `AnExplicitDeviceIsTheOwnersGpu` | ✅ local, owner's machine only |
| D2 (`Gpu`): on CUDA, `Exp`, `Log`, `Pow(x, 1.37)`, `Sqrt` within 4 ULP of `System.Math` on 10⁴ arguments | `OnCudaTheFourFunctionsAreWithinFourUlpOfSystemMath` | ❌ red: see BOOT.md |
| The same probe on OpenCL runs; its distances are reported, not held to a tolerance | `OnOpenClTheProbeRunsAndItsDistancesAreReported` | ✅ informative |
| The ULP distance is right at neighbours, across an exponent boundary and across zero | `UlpTests` | ✅ |

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
```

Internal helpers: `DevicePresence` (does ILGPU see a CUDA or OpenCL device), `Ulp`
(distance on the ordered bit patterns), `SumOfSquares`.
