# BOOT.md — GPU.Test/Bookkeeping

## Purpose

The work between generations of the GPU package's
[Bookkeeping](../../../src/DotNetDifferentialEvolution.GPU/Bookkeeping/API.md), held to the
CPU package's classes: checks **S7** (adaptation), **S8** (archive), **S9**
(ranking), **S10** (best index), **S11** (L-SHADE's reduction), **S12** (stagnation), **A3**
(ranking by integer keys and its calibrated limit) and **A4** (the wide passes and their
chunk) of
[Bookkeeping/ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/Bookkeeping/ACCEPTANCE.md).
The checks are frozen; their numbers are copied, never chosen here.

| Check | Test | How |
|---|---|---|
| S7 | `AdaptationParityTests` | the rules against `JadeStrategy`, `ShadeStrategy`, `LShadeStrategy` after `AfterGeneration`, read back through `GetControlParameters`; the kernels against the rules in chunks |
| S8 | `ArchiveParityTests` | the rule against `UpdateArchive` through `JadeStrategy` on a recording provider; the kernels against the rule on stream-1 draws |
| S9, S10 | `RankingTests` | both rankings and the best-index passes against host orders and `BestPick` |
| S11 | `LShadeReductionTests` | the schedule's known answers; a CPU-package and a GPU run side by side; the reduction pass |
| S12 | `StagnationTests` | the rule against `StagnationStreakTerminationStrategy`; runs that stop |
| A3 (CI) | `FitnessOrderTests` | `OrderKey`'s known answers, every NaN, 20 000 random pairs against `KeyOf` |
| A3 ⚠ (CI) | `RankingCalibrationTests` | `LimitOf` against a scripted time that records the n asked |
| A3 ⚠, A4 ⚠ (CI) | `RankingTests`, `WideChunkTests` | the CPU instance's L = 2 048 and a forced limit; `WideChunkSizeOf`'s known answers, the instance's fixed chunk, the best index under forced chunks |
| A3 ⚠, A4 ⚠ (**Gpu**) | `BookkeepingTimingTests` | calibrated L = 4 096 (CUDA) and 1 024 (OpenCL) at N_init 8 192, the times printed; `FindBest`'s device time through `OpenForTiming` at 1 024 (≤ 25 µs) and at 46 080 (c(N) against forced 1 024 and 32) |

## Invariants

- **The CPU package is the reference, run as its engine runs it**: a `ProblemContext`
  and `GenerationContext` over the given parents (`CpuGeneration`), the strategies'
  public `AfterGeneration`, `GetControlParameters` and `UseRandomProvider`. Nothing of
  the CPU package is copied into a test.
- **The CPU state is read back exactly.** A slot or mean is read through
  `GetControlParameters` with a provider that returns the slot, then 0, 0 (the
  Gaussian's first uniform is 1, so its normal is ±0) and ½ (the Cauchy's tangent is 0);
  a terminal slot draws no Gaussian, and its Cauchy turns the two zeros into redraws.
- **The device runs on ILGPU's CPU accelerator** through the package's own
  `GenerationBookkeeping`, whose buffers the tests fill and read directly.
- **Bitwise.** Every comparison of doubles is on their bits (`ParityCases`).

## Dependencies

- [Bookkeeping](../../../src/DotNetDifferentialEvolution.GPU/Bookkeeping/API.md) — the rules, `GenerationBookkeeping`, `BookkeepingPlan`, `SuccessSums`, `FitnessOrder`, `RankingCalibration`, `RankingTimes`, `RankingMeasurement`, `BookkeepingTuning`, `BookkeepingKernels.WideChunkSizeOf` (internal).
- [Devices](../../../src/DotNetDifferentialEvolution.GPU/Devices/API.md) — `DeviceSelector.Open`, `OpenForTiming`, `Backend`, `AcceleratorLease` (internal): CUDA and OpenCL for the **Gpu** checks of A3 and A4.
- [Kernels](../../../src/DotNetDifferentialEvolution.GPU/Kernels/API.md) — `PopulationViews`, `StrategyViews`, `ParameterRule`, `SchemeKind`, `Selection` (internal).
- [Random](../../../src/DotNetDifferentialEvolution.GPU/Random/API.md) — `PhiloxDraws` on the archive's stream, `IDrawSource` (internal).
- [Objectives](../../../src/DotNetDifferentialEvolution.GPU/Objectives/API.md) — `IGpuFitnessFunction`, `GeneView`.
- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md) — the builder, the result, the observer; internally `GpuBuilder<T>.WithStopReadInterval`, `RunSettings`, `BestPick`.
- [GPU.Test/Kernels](../Kernels/API.md) — `HostStep`, `ParityCases`, `ScriptedDraws`, `RecordingRandomProvider`.
- [DotNetDifferentialEvolution](../../../src/DotNetDifferentialEvolution/API.md) — the CPU package: its strategies, `ProblemContext`, `GenerationContext`, `TrialRecord`, `Population`, `PopulationSortHelper`, `StagnationStreakTerminationStrategy`, the builder.
- [Algorithms/Common](../../../src/DotNetDifferentialEvolution/Algorithms/Common/API.md) — the CPU package: `AdaptiveStrategyBase`, the adaptive strategies' base
- [Algorithms/Jade](../../../src/DotNetDifferentialEvolution/Algorithms/Jade/API.md) — the CPU package: `JadeStrategy`
- [Algorithms/Lshade](../../../src/DotNetDifferentialEvolution/Algorithms/Lshade/API.md) — the CPU package: `LShadeStrategy`
- [Algorithms/Shade](../../../src/DotNetDifferentialEvolution/Algorithms/Shade/API.md) — the CPU package: `ShadeStrategy`
- [ControlParameterProviders](../../../src/DotNetDifferentialEvolution/ControlParameterProviders/API.md) — the CPU package: `IControlParameterProvider`, `GetControlParameters`
- [GenerationStrategies](../../../src/DotNetDifferentialEvolution/GenerationStrategies/API.md) — the CPU package: `GenerationContext`
- [Helpers](../../../src/DotNetDifferentialEvolution/Helpers/API.md) — the CPU package: `PopulationSortHelper`
- [Interfaces](../../../src/DotNetDifferentialEvolution/Interfaces/API.md) — the CPU package: the observer the CPU builder takes
- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — the CPU package: `Population`, `ProblemContext`, `TrialRecord`
- [MutationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md) — the CPU package: `IMutationStrategy`
- [RandomProviders](../../../src/DotNetDifferentialEvolution/RandomProviders/API.md) — the CPU package: `SeededRandomProvider`
- [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md) — the CPU package: the greedy selection the CPU runs use
- [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md) — the CPU package: `StagnationStreakTerminationStrategy`, the limits
- [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md) — the CPU package: `ITerminationStrategy`

Outside the tree: ILGPU 1.5.3 (the CPU accelerator; profiling markers for the **Gpu** timings),
xUnit 2.9.3.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Every case carries a category. Only `BookkeepingTimingTests` needs a GPU (category `Gpu`,
  CUDA and OpenCL, this machine's devices, run by hand); every other case runs in CI on the
  CPU accelerator.

## Acceptance criteria

- [x] S7–S12 are green and each was red once on its named mutation: 2026-10-05, the
      evidence and the mutations in [Bookkeeping/ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/Bookkeeping/ACCEPTANCE.md).
- [x] A3 ⚠ and A4 ⚠ are green, in CI and under **Gpu**, and each was red on its named
      mutations: 2026-10-10, the evidence in the same file.

## Taboos

- **No tolerance**: the parity is bitwise or it is not parity.
- **No CPU-package formula retyped** in a test: the CPU class computes the reference.
