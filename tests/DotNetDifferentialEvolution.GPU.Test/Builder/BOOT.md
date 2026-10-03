# BOOT.md — GPU.Test/Builder

## Purpose

The GPU package's builder and `Build`, against the v1 contract: every argument error of
`API.md`'s "Errors of v1" table that a builder stage or `Build` raises (check B1), and
the initial population `Build` samples and evaluates (check 1a). The checks are those of
the package's [ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md),
frozen before the code; their numbers are copied, never chosen here.

## Invariants

- **Each B1 row has its own case**, asserting the exact exception type, and each guard
  has a boundary case beside it that passes, so a guard wider than the contract shows.
- **The rows `Build` cannot reach on every machine live elsewhere**: an absent explicit
  device in [Devices](../Devices/BOOT.md); `RunAsync` during a run and a throwing
  observer in [EndToEnd](../EndToEnd/BOOT.md).
- **1a reads the population the package's own kernel writes**: `KernelLauncher<Sphere>`
  runs `GpuKernels.Initialize` on ILGPU's CPU accelerator into buffers laid out as
  `GpuDifferentialEvolution` lays them out (seed 1, N = 1000, D = 3, [−2, 5]). The
  evaluation count is read from `Build`'s optimizer (`EvaluationCount`, internal).
- **The χ² threshold is computed, not typed**: `ChiSquared.Quantile(0.999, 19)` of
  [Random](../Random/API.md), whose closed-form controls include Γ(9.5), the
  half-integer behind df = 19. (This node had its own copy of the helper until
  2026-10-03; it was merged into Random's, the Γ(9.5) control with it.)
- **An accelerator of another type is hand-made** (`ForeignAccelerator`, type 99,
  through ILGPU's public `DeviceTypeAttribute`): ILGPU 1.5.3 defines only CPU, Cuda
  and OpenCL. It is never run.
- **The uncompilable objective holds a `throw`.** Measured 2026-10-03: ILGPU rejects it
  with `InternalCompilerException` on the CPU accelerator, CUDA and OpenCL alike, so the
  case runs in CI. A small `new double[2]` compiled and ran on all three, so it is not
  used.

## Dependencies

- [Kernels](../../../src/DotNetDifferentialEvolution.GPU/Kernels/API.md) — the DE step and the kernels (internal).
- [Objectives](../../../src/DotNetDifferentialEvolution.GPU/Objectives/API.md) — `IGpuFitnessFunction`, `GeneView`.
- [Random](../Random/API.md) — `ChiSquared`, the quantile helper.
- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md)
  — the builder and `GpuDifferentialEvolution`; internally `KernelLauncher<TFunction>`,
  `PopulationViews`, `StepParameters`, `DeStep.CrossoverThreshold` (its child
  documents do not exist yet).

Outside the tree: ILGPU 1.5.3 (`Context`, the CPU accelerator, `Device`,
`DeviceTypeAttribute`, `InternalCompilerException`), xUnit 2.9.3.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- No case needs a GPU: everything runs on ILGPU's CPU accelerator, in CI.
- Objective structs are `internal`, never private nested: ILGPU's `ILGPURuntime`
  assembly must see them.

## Acceptance criteria

- [x] Green: 2026-10-03, 41 cases in 3 classes, CPU accelerator, Release, local. 1a's
      statistics: genes 0, 1, 2 give χ² = 21.52, 19.96, 20.60 against the computed
      0.999 quantile 43.8202 (df = 19).
- [x] 1a is non-degenerate: 2026-10-03, each mutation applied to `src` in this worktree,
      observed, then restored with `git checkout`. The span written as `upper` instead of
      `upper − lower` turned `EachGenePassesAChiSquaredTestOnTwentyBins` red (gene 0:
      χ² = 399.92, the top five bins empty); the box case stays green under it, as it
      must ([−2, 3) is inside the box). A span of `upper − lower + 1` turned
      `EveryGeneIsInTheBox` red (a gene at 5.1221). Not setting the evaluation count in
      the constructor turned `BuildCostsNEvaluations` red.
- [x] B1 is non-degenerate: 2026-10-03, same procedure. Each guard of `GpuBuilder`
      weakened in turn (lengths, empty, lower > upper, non-finite, N ≥ 4 → N ≥ 3, N·D
      against `uint.MaxValue`, F, CR, both limits, the period, both `null` checks,
      `Enum.IsDefined`, the accelerator type) turned exactly its case red; the
      constructor wrapping ILGPU's exception in an `InvalidOperationException` turned
      `AnObjectiveIlgpuCannotCompileFailsBuildWithIlgpusException` red.
- [ ] ⚠ The accelerator-type guard is reachable only through a hand-made accelerator:
      the case depends on ILGPU keeping `DeviceTypeAttribute` public.

## Taboos

- **No threshold typed from a table or from memory**, and none loosened to pass.
- **No B1 case that accepts a base exception type** (`ThrowsAny`): the row names the type.
