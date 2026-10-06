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
| best/1, current-to-best/1, rand/2, best/2 and rand/1 build the CPU strategies' trials bit for bit from the same draws, 100 cases each (S2) | `SchemeParityTests` | ✅ |
| current-to-pbest/1 is `CurrentToPBestMutationStrategy`'s, with the archive drawn into and `topCount` clamped (S3) | `PBestParityTests` | ✅ |
| jDE's, JADE's, SHADE's and L-SHADE's F and CR are the CPU strategies', 10⁴ draws each, redraws and clamps counted (S4) | `ControlParameterParityTests` | ✅ |
| Selection outcomes are the CPU `SelectionStrategy`'s with ties accepted and refused; each configuration's tie rule is its CPU variant's (S5) | `SelectionOutcomeTests`, `TieRuleTests` | ✅ |
| jDE's F and CR follow the trial exactly where it improved on the parent; a tie keeps the parent's (S6) | `JdeInheritanceTests` | ✅ |

## Tests ✅

```csharp
[Trait("Category", "Integration")] public class DonorPickTests;
[Trait("Category", "Unit")] public class CrossoverTests;
[Trait("Category", "Unit")] public class RepairTests;
[Trait("Category", "Unit")] public class SurvivalTests;
[Trait("Category", "Unit")] public class BestPickTests;
[Trait("Category", "Unit")] public class CpuParityTests;
[Trait("Category", "Integration")] public class GenerationSlotTests;
[Trait("Category", "Unit")] public class SchemeParityTests;
[Trait("Category", "Unit")] public class PBestParityTests;
[Trait("Category", "Unit")] public class ControlParameterParityTests;
[Trait("Category", "Unit")] public class SelectionOutcomeTests;
[Trait("Category", "Integration")] public class TieRuleTests;
[Trait("Category", "Integration")] public class JdeInheritanceTests;
```

Helpers, internal to the node: `ScriptedDraws` (with `ScriptedDraw`, `DrawKind`),
`HostStep`, `RecordingRandomProvider`, `DonorPickKernel`, `ParityCases` (random cases and
bitwise asserts), `SchemeState` (the best index, ranking and archive a scheme reads).
