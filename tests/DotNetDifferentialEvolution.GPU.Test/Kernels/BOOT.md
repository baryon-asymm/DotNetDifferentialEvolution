# BOOT.md — GPU.Test/Kernels

## Purpose

The semantics of one DE step on the GPU (`docs/ALGORITHMS.md` §§2–3, §9) and the
kernel's slot discipline: ACCEPTANCE.md checks **1b** (donors), **1c** (crossover with
`jrand`), **1d** (midpoint repair), **1e** (selection), **1f** (best pick), **1g**
(parity with the CPU package) and **2b** (slot i holds parent i or trial i).

| Check | Test | How |
|---|---|---|
| 1b | `DonorPickTests` | test kernel `DonorPickKernel` on the CPU accelerator, 10⁵ picks per i, N = 4 and 50 |
| 1c | `CrossoverTests` | `DeStep.BuildTrial` on the host over CPU-accelerator buffers, `ScriptedDraws` |
| 1d | `RepairTests` | the same, mutants out of the box on both sides |
| 1e | `SurvivalTests` | `DeStep.Survives`, the fitness values of the CPU `SelectionStrategyTests` |
| 1f | `BestPickTests` | `BestPick.IndexOf([NaN, 3, 1, 1])` |
| 1g | `CpuParityTests` | CPU `MutationStrategy.Mutate` on `RecordingRandomProvider`, replayed into the GPU step |
| 2b | `GenerationSlotTests` | `KernelLauncher` `Initialize` + one `Generation`, N = 64, D = 4, recomputed on the host |

## Invariants

- **Scripted draws are exact.** `ScriptedDraws` throws on a draw of the wrong kind, an
  index outside `[0, n)`, a range other than the scripted one, or an exhausted script,
  so a test sees the step consume exactly the draws it was given.
- **Expected values are closed forms**, typed by hand from `ALGORITHMS.md`, with
  inputs chosen so the arithmetic is exact (F = ½ or 1, small integers).
- **1b's uniformity is one χ² per (N, role)** over all (i, index ≠ i) cells, df =
  N·(N − 2): 8 for N = 4, 2400 for N = 50, at the 0.999 quantile of
  [`ChiSquared`](../Random/API.md). Six joint tests rather than 162 per-(N, i, role)
  ones, which at 0.999 each would fail by chance about one run in seven.
- **1e mirrors eight of the nine CPU cases.** The GPU package has no ties-rejected
  mode: `WithTiesRejectedKeepsTheParentOnEqualFitness` has no counterpart (⚠ for
  ACCEPTANCE.md); `WithTiesRejectedStillTakesAStrictlyBetterTrial` and
  `WithTiesRejectedAParentScoredNaNIsStillReplaced` have the same survivor under the
  GPU rule and are mirrored.
- **1g's draws are exact multiples of 2⁻⁵³**, so the CPU's
  `RandomThreshold.Scale(d)` is exactly `m << 11`, the word replayed to the GPU.
  Parity is bitwise (`DoubleToInt64Bits`), and the call kinds and ranges match one to
  one (`ScriptedDraws` with the recorded ranges; total counts asserted).
- **1g exercises the repair**: a replay with unbounded boxes counts the genes repaired
  below and above, and both counts must be positive.

## Dependencies

- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md) —
  internal `DeStep`, `GpuKernels`, `KernelLauncher<T>`, `PopulationViews`,
  `StepParameters`, `BestPick`, `IDrawSource`, `PhiloxDraws`; public `GeneView`,
  `IGpuFitnessFunction`.
- [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md)
  — `MutationStrategy`, `MutationContext` (check 1g).
- [RandomProviders](../../../src/DotNetDifferentialEvolution/RandomProviders/API.md) —
  `SeededRandomProvider`, the source of 1g's cases and draws.
- [Random](../Random/API.md) — `ChiSquared` (check 1b).

Outside the tree: ILGPU 1.5.3 (CPU accelerator), DotNetOptimization.Abstractions
(`BaseRandomProvider`), xUnit.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- A kernel-compiled type of the tests (`DonorPickKernel`,
  `GenerationSlotTests.ShiftedSphere`) is internal, never private: ILGPU's
  `ILGPURuntime` assembly sees internals only.
- `Unit` for host calls, `Integration` for a kernel launch on the CPU accelerator; no
  test here needs a GPU.

## Acceptance criteria

- [x] Green: 2026-10-03, 19 cases in 7 test classes, on the host or on ILGPU's CPU accelerator.
- [x] 1b red: 2026-10-03, scratch mutation `DrawOther` returning `candidate` (no skip of
      i): both N fail on the distinctness assertion (`DonorPickTests.cs` line 61,
      300 000 and 300 376 violations). A further, non-frozen bias (index 0 redrawn
      once) keeps distinctness and fails on χ² (line 80: N = 4, χ² 88 790 against
      26.12, df 8; N = 50, χ² 102 445 against 2619.8, df 2400).
- [x] 1c red: 2026-10-03, `|| j == jrand` removed: the CR = 0 case fails on
      `CrossoverTests.cs` line 43 (`[10, 20, 30, 40, 50]` against
      `[10, 20, 4, 40, 50]`); the other two fail on an exhausted script.
- [x] 1d and 1g red: 2026-10-03, repair as a clamp to the bound: `RepairTests.cs`
      lines 29 and 33 (−2 against −0.5, 12 against 10.5), `CpuParityTests.cs` line 81
      (case 1, gene 0); the in-box case stays green.
- [x] 1e red: 2026-10-03, `<=` as `<`: the tie fails (`SurvivalTests.cs` line 29);
      `<=` alone, no NaN clause: both NaN-parent cases fail (lines 37 and 41); `<` alone
      fails all three.
- [x] 1f red: 2026-10-03, NaN clause removed from `BestPick`: 0 against 2
      (`BestPickTests.cs` line 13).
- [x] 2b red: 2026-10-03, `GpuKernels.Generation` writing next slot `(i + 1) % N`:
      `GenerationSlotTests.cs` line 68, "slot 0 is neither its parent nor its trial".

## Taboos

- **No expected value taken from the code under test.**
- **No lenient script**: a draw the test did not script is a failure, not a default.
- **No per-(N, i, role) χ² at 0.999** without stating the family-wise rate.
