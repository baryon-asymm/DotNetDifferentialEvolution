# API.md — GPU.Test/Bookkeeping

Nothing outward. What this node proves about the GPU package's work between generations.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| JADE's means and the SHADE and L-SHADE memories are the CPU strategies' bit for bit; the kernels are the rules in chunks of 1 024 (S7) | `AdaptationParityTests` | ✅ |
| The archive is the CPU package's `UpdateArchive` given the same slot draws; the kernels are the rule (S8) | `ArchiveParityTests` | ✅ |
| Both rankings give the order by (key, index), `NaN` as +∞ (S9); the best index is `BestPick`'s (S10) | `RankingTests` | ✅ |
| L-SHADE's sizes are the known answers and the CPU package's; the reduction keeps the ranking's first N (S11) | `LShadeReductionTests` | ✅ |
| The stop rule is the CPU package's; a run stops where it says, whatever the read interval (S12) | `StagnationTests` | ✅ |
| `OrderKey` orders −∞, −`double.MaxValue`, −1, −ε, −0, +0, ε, 1, `double.MaxValue`, +∞, NaN non-decreasingly with −0 = +0 and +∞ = NaN, as `KeyOf` orders any two doubles; `Precedes` is (key, index) (A3, CI) | `FitnessOrderTests` | ✅ |
| `RankingCalibration.LimitOf` against a scripted `time` that records the n it was asked: N_init ≤ 1 024 → 1 024 and never asked; faster at 2 048 and 4 096, slower at 8 192 → 4 096; slower at 2 048 → 1 024; faster everywhere with N_init 3 000 → 3 000 (asked at 2 048 and 3 000 only), with 20 000 → 8 192; the first slower n ends the search (A3 ⚠, CI) | `RankingCalibrationTests` | ✅ |
| The CPU accelerator's instance has L = 2 048 and times nothing; a limit forced through `BookkeepingTuning` decides the ranking and times nothing; `RankingTests` straddles the instance's limit (A3 ⚠, CI) | `RankingTests` | ✅ |
| `WideChunkSizeOf` is 32 for N_init 1 and 1 024, 64 for 1 025, 128 for 16 384, 224 for 46 080; an instance keeps the chunk of its N_init (an L-SHADE instance of 1 025 keeps 64 when it finds the best of 1 024, and its result is `BestPick`'s); the best index does not depend on the chunk a test forces (A4 ⚠, CI) | `WideChunkTests` | ✅ |
| `Gpu`: an instance with a ranking plan and N_init = 8 192 calibrates L = 4 096 on CUDA (RTX 5070 Ti) and L = 1 024 on OpenCL (gfx1036), the measured times printed (A3 ⚠) | `BookkeepingTimingTests` | ✅ 2026-10-10, the orchestrator's runs |
| `Gpu`, CUDA, device time through `DeviceSelector.OpenForTiming` (median of 20 calls after 2 warm-ups, between two profiling markers, printed): the best index at N = 1 024 takes at most 25 µs; at N = 46 080 `c(N)` is not slower than chunks of 1 024 nor than chunks of 32, both forced through `BookkeepingTuning` (A4 ⚠) | `BookkeepingTimingTests` | ✅ 2026-10-10, the orchestrator's runs |

## Tests ✅

```csharp
[Trait("Category", "Integration")] public class AdaptationParityTests;
[Trait("Category", "Integration")] public class ArchiveParityTests;
[Trait("Category", "Integration")] public sealed class RankingTests : IDisposable;
[Trait("Category", "Integration")] public class LShadeReductionTests;
[Trait("Category", "Integration")] public class StagnationTests;
[Trait("Category", "Unit")] public class FitnessOrderTests;
[Trait("Category", "Unit")] public class RankingCalibrationTests;
[Trait("Category", "Integration")] public sealed class WideChunkTests : IDisposable;
[Trait("Category", "Gpu")] public class BookkeepingTimingTests;
```

`RankingTests` builds its bookkeeping with an L-SHADE-shaped plan, which loads both rankings, since
the plan decides which kernels are loaded (A10); its arrays straddle the instance's ranking limit
(2 048 on the CPU accelerator) and its best-index ties straddle the chunk boundaries of 32, of
1 024 and of the instance's own wide chunk. `WideChunkTests` forces the wide chunk through the
internal constructor. `BookkeepingTimingTests` opens CUDA and OpenCL through `DeviceSelector`
(`Open` for the calibration, `OpenForTiming` for the device-time checks) and reads the times the
calibration measured from `GenerationBookkeeping.RankingMeasurements`.

Helpers, internal to the node: `CpuGeneration` (the CPU contexts, records and a scripted
provider), `HostArchive` (the archive's rule on the host).
