# BOOT.md — DotNetDifferentialEvolution.GPU.Test

## Purpose

What "the GPU package is ready" means: the frozen checks of its
[ACCEPTANCE.md](../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md), each proven on a
known answer and red on a named mutation. The kernel guards (checks 5a, 7a, 8a–8d) live in
[Protocol.Tests](../DotNetDifferentialEvolution.Protocol.Tests/API.md); everything else is
here. The tests of 0.x left with its code → HISTORY.md#tests-of-0x-2026-10-03.

| Level | What it checks | Against what | Child |
|---|---|---|---|
| G0 | the RNG: known answers, uniformity, index draws, the same words on every backend | Random123's KAT vectors; closed forms; χ² quantiles computed and checked | [Random](Random/API.md) |
| G1 | one DE step: donors, crossover, repair, survival, best pick, slot discipline | closed forms; the CPU package's step, bit for bit | [Kernels](Kernels/API.md) |
| G1 | the objective's view cannot write | reflection | [Objectives](Objectives/API.md) |
| G2 | the builder's argument errors, the device choice, the math probe | the error table of the package's `API.md`; `System.Math` | [Builder](Builder/API.md), [Devices](Devices/API.md) |
| G3 | whole runs: sampling, convergence, reproducibility, transfers, asynchrony, ownership | known optima; counts | [EndToEnd](EndToEnd/API.md) |

## Invariants

- **Every check runs on ILGPU's CPU accelerator** in hosted CI. A check that needs a real
  device carries `[Trait("Category", "Gpu")]`, which CI excludes; it runs on a developer
  machine with CUDA and OpenCL.
- **No expected value is taken from an optimizer run.** Known answers are published
  vectors, closed forms, or optima of the objective.
- **Statistical thresholds are computed, not typed.** `Random/ChiSquared` computes the
  quantiles and is checked against closed forms itself.
- **Objectives compiled into kernels are `internal` structs.** The project grants
  `InternalsVisibleTo("ILGPURuntime")` so ILGPU's launchers can see them.

## Dependencies

- [DotNetDifferentialEvolution.GPU](../../src/DotNetDifferentialEvolution.GPU/API.md) — the
  package under test, its internals included (`InternalsVisibleTo`).

Each child declares the package nodes it checks. Outside the tree: xUnit 2.9.3,
xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.0;
ILGPU 1.5.3. The project also references the CPU package, for check 1g only; the GPU package
does not.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings come from `tests/Directory.Build.props`.
- A red result on a frozen check is not fixed in the test: the check is changed only openly,
  with ⚠ and the reason, in the package's `ACCEPTANCE.md`.

## Acceptance criteria

- [x] The project builds with 0 warnings under the maximum diagnostics and every test
      passes, the `Gpu` ones included: 2026-10-03, local, Windows 11, RTX 5070 Ti (CUDA)
      and `gfx1036` (OpenCL); counts in the package's `ACCEPTANCE.md`.
- [x] Every check was seen red on its named mutation, once, then restored: 2026-10-03,
      listed per check in the package's `ACCEPTANCE.md`.

## Taboos

- **No looser tolerance or threshold for the sake of green.**
- **No expected value taken from an optimizer run.**
- **No device test without `Category=Gpu`.** CI would run it on runners without a GPU.
- **No `Skip`, no hidden theory data.** Held by `NoSuppressionGuardTests` in Protocol.Tests.
