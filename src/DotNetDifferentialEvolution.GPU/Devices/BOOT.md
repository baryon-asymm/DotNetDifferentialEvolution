# BOOT.md — DotNetDifferentialEvolution.GPU/Devices

## Purpose

Which accelerator a run gets, and who owns it. `Auto` tries CUDA, then OpenCL, then
ILGPU's CPU accelerator and keeps the reason each skipped backend gave; an explicit
backend is that backend or an error. Also the math probe that holds a device's
transcendental functions against `System.Math`.

## Invariants

- **An explicit device never falls back.** A missing CUDA or OpenCL device makes `Build`
  throw `InvalidOperationException` naming it. Held by ACCEPTANCE.md, check D1.
- **A skipped backend leaves its reason.** `FallbackReason` is null exactly when nothing
  was skipped. Held by check D1.
- **A caller's accelerator is never disposed**; an opened one is disposed with its
  context. Held by check 7b.
- **Every context gets `EnableAlgorithms()`**, so `Exp`, `Log` and `Pow` compile on PTX.

## Dependencies

None.

Outside the tree: ILGPU 1.5.3 and ILGPU.Algorithms 1.5.3.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Auto falls back when a backend has no device or its accelerator cannot be created. It
  does not fall back when the kernels later fail to compile on the device it opened:
  that surfaces from `Build`.
- The CPU accelerator is ILGPU's default CPU device, not one sized from
  `ProcessorCount`; it is a test oracle and a fallback, not the fast path.

## Acceptance criteria

→ checks 7b, D1 and D2 of the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Taboos

- **No silent fallback from an explicit device.**
- **No disposal of an accelerator the optimizer did not create.**
