# BOOT.md — GPU.Test/Devices

## Purpose

The GPU package's device selection and its CUDA math probe: checks D1 and D2 of the
package's [ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md),
and B1's row "an explicit device that is not present". The checks are frozen; their
numbers (4 ULP, 10⁴ arguments) are copied, never chosen here.

## Invariants

- **The no-device cases follow the machine.** `DevicePresence` asks ILGPU, with the
  options the package uses, whether a CUDA or an OpenCL device is present, and each case
  asserts the branch that matches: on a hosted runner, D1's frozen assertions; on a
  machine with the device, that the explicit request is honoured with no fallback.
  These cases carry no category and run in CI.
- **Both branches are exercised locally** by hiding the GPUs from the test process:
  `CUDA_VISIBLE_DEVICES=-1` hides CUDA (and NVIDIA's OpenCL device);
  `GPU_DEVICE_ORDINAL=7` hides AMD's OpenCL device. With both set, `Auto` gives the CPU
  accelerator. Measured 2026-10-03.
- **The `Gpu` cases are specific to the owner's machine**: D1 names the RTX 5070 Ti
  (CUDA) and `gfx1036` (OpenCL), as the check itself does. They fail elsewhere by design.
- **D2 opens CUDA through the package's `DeviceSelector`**, so `MathProbe.Probe`
  compiles with the options a run uses (`EnableAlgorithms`). The arguments are a
  log-spaced grid of 10⁴ points over [1e-3, 700]; the distance is counted on the
  ordered IEEE 754 bit patterns (`Ulp`), whose own positive controls use
  `Math.BitIncrement`/`BitDecrement`.
- **OpenCL is measured, not judged**: D2 names CUDA only, so the OpenCL case asserts
  only that every result is a number and reports the distances.

## Dependencies

- [Devices](../../../src/DotNetDifferentialEvolution.GPU/Devices/API.md) — `DeviceSelector`, `AcceleratorLease`, `MathProbe` (internal).
- [Objectives](../../../src/DotNetDifferentialEvolution.GPU/Objectives/API.md) — `IGpuFitnessFunction`, `GeneView`.
- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md)
  — the builder, `GpuDeviceInfo`; internally `DeviceSelector`, `Backend`, `MathProbe`
  (its child documents do not exist yet).

Outside the tree: ILGPU 1.5.3 and ILGPU.Algorithms 1.5.3 (`Context`, CUDA, OpenCL),
xUnit 2.9.3; for the `Gpu` cases, the owner's RTX 5070 Ti and `gfx1036`.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- A `Gpu` case is marked on the method; CI filters `Category!=Gpu`.

## Acceptance criteria

- [x] D1 and B1's device row are green on every branch: 2026-10-03, local. With the
      GPUs visible, CUDA hidden (`CUDA_VISIBLE_DEVICES=-1`), and both hidden (plus
      `GPU_DEVICE_ORDINAL=7`): 4 of 4 each. Hidden, `Auto` gave `Cpu` with the reason
      "CUDA: no such device is present.; OpenCL: no such device is present.", and an
      explicit `Cuda` threw "The CUDA device was requested and cannot be used: …".
      Under `Gpu`: CUDA is "NVIDIA GeForce RTX 5070 Ti", OpenCL is "gfx1036".
- [x] D1 and B1's device row are non-degenerate: 2026-10-03, each mutation applied to
      `src` in this worktree, observed, then restored. An explicit device falling back to
      `Auto` turned the three explicit-device cases red (GPUs hidden); a message without
      the device's name turned the same three red; a `null` fallback reason turned
      `AutoFallsBackToTheCpuWithAReasonWhenNoGpuIsPresent` red with both GPUs hidden and
      with CUDA hidden; `Cuda` mapped to the OpenCL backend turned the `Gpu` CUDA case red.
- [x] The ULP helper is right: 2026-10-03, 8 cases of `UlpTests`; the OpenCL probe gives
      at most 1 ULP for all four functions, so the measurement is not the source of D2's
      distances.
- [ ] ⚠ **D2 is red** (2026-10-03, RTX 5070 Ti, ILGPU 1.5.3 with `EnableAlgorithms`):
      largest distance from `System.Math` over the 10⁴ arguments — `Exp` 195 ULP
      (x = 652.2457760772028; 2618 arguments over 4 ULP), `Log` 9430 ULP
      (x = 0.9999920999476787; 520 over), `Pow(x, 1.37)` 24 ULP
      (x = 0.0013455021986893204; 2807 over), `Sqrt` 0. OpenCL `gfx1036`: 1, 1, 1, 0.
      Nothing was loosened; the check stops here until the owner decides.
- [ ] ⚠ The hosted-runner branch was run here only with the GPUs hidden by environment
      variables, not yet on a hosted runner.

## Taboos

- **No looser ULP tolerance and no narrower argument range** to make D2 green: the 4 ULP
  are APT's measurement for libdevice.
- **No device name made generic** in the `Gpu` case: the check names the devices.
