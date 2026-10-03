# API.md — GPU.Test/Kernels

Nothing outward. What this node proves about the DE step of the GPU package.

## What this node guarantees

| Claim | Confirmed by | State |
|---|---|---|
| r1, r2, r3 are distinct and never i; each role is uniform over the others (1b) | `DonorPickTests` | ✅ |
| CR = 0 takes exactly `jrand`, CR = 1 every gene, a mix the closed form (1c) | `CrossoverTests` | ✅ |
| Out-of-box genes become the midpoint with the parent; in-box genes are untouched (1d) | `RepairTests` | ✅ |
| Survival is `f(u) ≤ f(x)` with NaN worst and two NaNs no tie, as in eight of the CPU's nine cases (1e) | `SurvivalTests` | ✅ |
| The best index ranks NaN worst and gives a tie to the lowest index (1f) | `BestPickTests` | ✅ |
| The same draws give bit-identical trials on GPU and CPU, 100 random cases (1g) | `CpuParityTests` | ✅ |
| After one generation, slot i holds parent i or trial i, as selection decides (2b) | `GenerationSlotTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Integration")] public class DonorPickTests;
[Trait("Category", "Unit")] public class CrossoverTests;
[Trait("Category", "Unit")] public class RepairTests;
[Trait("Category", "Unit")] public class SurvivalTests;
[Trait("Category", "Unit")] public class BestPickTests;
[Trait("Category", "Unit")] public class CpuParityTests;
[Trait("Category", "Integration")] public class GenerationSlotTests;
```

Helpers, internal to the node: `ScriptedDraws` (with `ScriptedDraw`, `DrawKind`),
`HostStep`, `RecordingRandomProvider`, `DonorPickKernel`.
