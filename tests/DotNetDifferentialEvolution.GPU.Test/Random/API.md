# API.md — GPU.Test/Random

Nothing outward but one helper: what this node proves about the GPU package's random
numbers, and the χ² helper a sibling uses.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| Philox4x32-10 gives Random123's three known answers on the host and in a kernel on the CPU accelerator (3a) | `PhiloxKnownAnswerTests` | ✅ |
| The same inside a kernel on CUDA and on OpenCL (3a, **Gpu**) | `DeviceDrawTests.AKernelOnTheDeviceGivesTheKnownAnswers` | ✅ local |
| 10⁶ unit doubles pass χ² on 100 bins at 0.999; the largest is exactly `1 − 2⁻⁵³` (3b) | `UnitDoubleTests` | ✅ |
| `ToIndex` is exactly `⌊word·n / 2³²⌋` (3c) | `IndexDrawTests` | ✅ |
| The first 10⁴ draws of a (seed, individual, generation) are bit-identical on host and CPU accelerator (4b) | `DrawSequenceTests` | ✅ |
| The same on CUDA and on OpenCL (4b, **Gpu**) | `DeviceDrawTests.TheDeviceDrawsTheHostWords` | ✅ local |

## Helper for siblings ✅

```csharp
internal static class ChiSquared
{
    public static double Statistic(ReadOnlySpan<long> observed, double expected);
    public static double Cdf(double x, int degreesOfFreedom);
    public static double Quantile(double probability, int degreesOfFreedom);
    public static double LogGammaOfHalf(int degreesOfFreedom);
}
```

## Tests ✅

```csharp
[Trait("Category", "Unit")] public class ChiSquaredTests;
[Trait("Category", "Unit")] public class UnitDoubleTests;
[Trait("Category", "Unit")] public class IndexDrawTests;
[Trait("Category", "Integration")] public class PhiloxKnownAnswerTests;
[Trait("Category", "Integration")] public class DrawSequenceTests;
[Trait("Category", "Gpu")] public class DeviceDrawTests;
```
