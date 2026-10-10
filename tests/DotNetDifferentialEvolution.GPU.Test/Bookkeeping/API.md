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

## Tests ✅

```csharp
[Trait("Category", "Integration")] public class AdaptationParityTests;
[Trait("Category", "Integration")] public class ArchiveParityTests;
[Trait("Category", "Integration")] public sealed class RankingTests : IDisposable;
[Trait("Category", "Integration")] public class LShadeReductionTests;
[Trait("Category", "Integration")] public class StagnationTests;
```

`RankingTests` builds its bookkeeping with an L-SHADE-shaped plan, which loads both rankings, since
the plan decides which kernels are loaded (A10).

Helpers, internal to the node: `CpuGeneration` (the CPU contexts, records and a scripted
provider), `HostArchive` (the archive's rule on the host).
