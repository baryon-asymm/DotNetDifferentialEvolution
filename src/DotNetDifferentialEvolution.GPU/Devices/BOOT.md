# BOOT.md — DotNetDifferentialEvolution.GPU/Devices

## Purpose

Which accelerator a run gets, who owns it, and how a kernel is loaded on it. `Auto` tries
CUDA, then OpenCL, then ILGPU's CPU accelerator and keeps the reason each skipped backend
gave; an explicit backend is that backend or an error. On CUDA the math is libdevice's,
completed by the post-link of the child node [LibDevice](LibDevice/API.md), as APThermo
does it (HISTORY.md#libdevice-port-2026-10-03). Also the math probe that holds a device's
transcendental functions against `System.Math`.

## Invariants

- **An explicit device never falls back.** A missing CUDA or OpenCL device makes `Build`
  throw `InvalidOperationException` naming it. Held by ACCEPTANCE.md, check D1.
- **A skipped backend leaves its reason.** `FallbackReason` is null exactly when nothing
  was skipped. Held by checks D1 and L6.
- **A caller's accelerator is never disposed**; an opened one is disposed with its
  context. Held by check 7b.
- **Every kernel of the package is loaded through `KernelLoader`**: on CUDA compiled,
  completed by the post-link and loaded; elsewhere loaded as ILGPU loads it. Held by
  checks D2 and L5.
- **CUDA counts as opened only after a kernel has loaded on it**: the math probe, through
  `KernelLoader`, released at once. Held by check L5.
- **libnvvm is checked before the CUDA accelerator exists**, so a bad library never reaches
  ILGPU's accelerator constructor, which would create a CUDA context and keep no handle to
  release it. Held by check L7.
- **No ILGPU.Algorithms.** A CUDA context gets `Math(MathMode.Default)` and
  `LibDevice(dll, bitcode)`; OpenCL and the CPU accelerator use their own math. Held by
  check L8.

## Dependencies

None.

Outside the tree: ILGPU 1.5.3; a CUDA Toolkit for CUDA (libnvvm and `libdevice.10.bc`,
the child's `BOOT.md`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- **When CUDA is tried**, in order: the ILGPU pin is asserted; libdevice is located; a
  context is created with the devices registered through `CudaWslDevices` and, when
  libdevice was found, `LibDevice`; no device → "no such device is present."; no libdevice
  → the reason names both files and the paths tried; libnvvm is loaded, asked its IR
  version and its bitcode read, then released; the accelerator is created; the probe
  kernel is loaded. Any failure is the skip reason under Auto, or the explicit request's
  error.
- **A failed open releases what it created in its own catch**, and a failure of that
  release is appended to the reason ("; releasing it also failed: …"), never put in its
  place: ILGPU 1.5.3's CUDA accelerator throws from `Dispose` after its loader failed a
  kernel (2026-10-03, the child's `BOOT.md`).
- Auto falls back when a backend has no device, no libdevice, or an accelerator or probe
  kernel that cannot be created. It does not fall back when the run's kernels later fail
  to compile on the device it opened: that surfaces from `Build`.
- **A caller-owned CUDA accelerator** is used as it is: its kernels go through the same
  post-link, with the libnvvm its own context loaded. A context built without `LibDevice`
  has none, so an objective calling `Exp`, `Log` or `Pow` fails `Build` there (the
  package's `API.md`).
- The CPU accelerator is ILGPU's default CPU device, not one sized from
  `ProcessorCount`; it is a test oracle and a fallback, not the fast path.

## Acceptance criteria

→ checks 7b, D1, D2 and L5–L8 of the package's [ACCEPTANCE.md](../ACCEPTANCE.md).

## Decomposition

| Node | Role |
|---|---|
| this node | `Backend`, `AcceleratorLease`, `DeviceSelector`, `KernelLoader`, `MathProbe` |
| [LibDevice](LibDevice/API.md) | libdevice discovery, the post-link, the WSL workaround |

This node uses its child; the child uses nothing of the package.

## Taboos

- **No silent fallback from an explicit device.**
- **No disposal of an accelerator the optimizer did not create.**
- **No kernel loaded around `KernelLoader`.**
