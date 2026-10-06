# BOOT.md — GPU.Test/EndToEnd

## Purpose

Whole runs of the GPU package through its public builder: checks 1h, 4a, 5b, 6a, 6b,
6c and 7b of the package's
[ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md), and B1's
rows "`RunAsync` while a run is in progress" and "the observer throws". The checks are
frozen; their numbers are copied, never chosen here.

## Invariants

- **Every check runs on ILGPU's CPU accelerator in CI**; 1h, 4a and 7b run again on
  CUDA and OpenCL under `Category=Gpu`.
- **1h's run settings were chosen once, before the first run, and are not tuned**:
  seed 1, N = 50, F = 0.5, CR = 0.9, 1000 generations; Sphere 5-D and Rosenbrock 2-D
  in [−5, 5], Rastrigin 2-D in [−5.12, 5.12]. The optima are analytic. Rastrigin's
  cosine is a reduced Taylor series (`Math.Cos` is not on the package's allow-list),
  checked on the host against `Math.Cos`.
- **4a compares bit patterns**: the final population from an observer due once at the
  last generation, and the result. Results are never compared across devices.
- **5b reads the package's own counter** (`PopulationDownloadCount`, internal).
- **Nothing is timed.** 6a and 6c hold the run on a gate the test opens
  (`GateObserver`); the observer signals entry first, then waits. If it is called on the
  thread that called `RunAsync`, it does not wait, so a synchronous `RunAsync` turns the
  case red instead of deadlocking it. Every wait is bounded by `HangGuard` (2 minutes),
  a guard against a hang, not a measurement.
- **7b also launches a kernel into the buffer and reads it back.** ILGPU's CPU
  accelerator allocates after its own `Dispose` (measured 2026-10-03) and fails only at
  a kernel launch; CUDA fails at allocation.

## Dependencies

- [Objectives](../../../src/DotNetDifferentialEvolution.GPU/Objectives/API.md) — `IGpuFitnessFunction`, `GeneView`.
- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md)
  — the builder, `GpuDifferentialEvolution`, `GpuOptimizationResult`,
  `GpuPopulationSnapshot`, `IGpuPopulationUpdatedHandler`; internally
  `PopulationDownloadCount`, `StopReadCount`, `GenerationEnqueued` (S18's hook),
  `GpuBuilder<T>.WithStopReadInterval` (its child documents do not exist yet).
- [DotNetDifferentialEvolution](../../../src/DotNetDifferentialEvolution/API.md) — the CPU package: the builder and its run, held beside the GPU runs (S13, S14)
- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — the CPU package: `Population`, the CPU run's result
- [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md) — the CPU package: `StagnationStreakTerminationStrategy`, the limits (S13, S17)
- [TerminationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/TerminationStrategies/Interfaces/API.md) — the CPU package: `ITerminationStrategy`

Outside the tree: ILGPU 1.5.3 (a caller-owned `Context` and accelerator for 7b),
xUnit 2.9.3; for the `Gpu` cases, a CUDA and an OpenCL device.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- The CPU set must stay fast: 2–3 s for the whole project on a quiet machine, 2026-10-03.
- Awaits use `ConfigureAwait(true)`; no `Thread.Sleep`, no `System.Random`.

## Acceptance criteria

- [x] Green: 2026-10-03, local, Release. CPU set 14 of 14; `Gpu` set 12 of 12 on
      CUDA (RTX 5070 Ti) and OpenCL (`gfx1036`). 1h reached, on every device: Sphere
      below 2e-88, Rosenbrock exactly 0 at (1, 1), Rastrigin exactly 0 with genes below
      1.4e-9. The CPU accelerator and OpenCL gave the same Sphere value bit for bit,
      CUDA a different one (not compared, invariant 4).
- [x] Non-degenerate: 2026-10-03, each mutation applied to `src` in this worktree,
      observed, then restored with `git checkout`. Selection that never takes the trial
      turned all 9 convergence cases red (Sphere 12.93, Rosenbrock 49.90, Rastrigin
      2.31); a seed from `RandomNumberGenerator` despite `WithSeed` turned the 3
      same-seed cases red; a download per generation turned both 5b cases red; the loop
      run on the caller's thread turned 6a red ("The observer ran on the thread that
      called RunAsync") and `ACallDuringTheRunThrows` red; the caller's token not
      observed turned 6b red; no reuse of the finished task turned both 6c cases red; a
      lease that always disposes turned 7b red on the CPU accelerator
      (`ObjectDisposedException` at the kernel launch), CUDA (`CudaException` at the
      allocation) and OpenCL (`CLException`); an observer's exception swallowed turned
      `AThrowingObserverFaultsTheTaskWithItsException` red.
- [ ] ⚠ 7b as worded ("still allocates a buffer") cannot fail on the CPU accelerator,
      which allocates after its own `Dispose`; the case adds a kernel launch and a
      read-back, which can.
- [ ] ⚠ The CPU accelerator's run time follows machine load: the whole CPU set took
      2–3 s on most runs and up to 53 s while other test processes ran on the machine;
      pinned to 2 cores, every case took at most 4 s. Nothing asserts time.
- [x] S13, S14 and S17 are green, on the CPU accelerator and (S13, S14) on CUDA, and each was
      red once on its named mutation: 2026-10-05, the evidence in the package's
      [ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md).
- [x] S18 is green on the CPU accelerator and was red on the code before it and on each of
      its two named mutations: 2026-10-06, the evidence in the package's
      [ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md).

## Taboos

- **No run setting of 1h tuned after a red run**, and no threshold loosened.
- **No timing assertion**: a wait that ends by a timeout is a failure, never a pass.
