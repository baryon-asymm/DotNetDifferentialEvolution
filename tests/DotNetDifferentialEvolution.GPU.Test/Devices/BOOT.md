# BOOT.md — GPU.Test/Devices

## Purpose

The GPU package's device selection, its CUDA math through libdevice, and its math probe:
checks D1, D2 and L1–L7, L9 of the package's
[ACCEPTANCE.md](../../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md), and B1's row
"an explicit device that is not present" (L8 lives in Protocol.Tests). The checks are
frozen; their numbers (4 ULP, 10⁴ arguments, 64 MiB) are copied, never chosen here.

## Invariants

- **The no-device cases follow the machine.** `DevicePresence` asks ILGPU whether a CUDA
  or an OpenCL device is present, and the package's locator whether a CUDA Toolkit is, and
  each case asserts the branch that matches: on a hosted runner, D1's and L6's no-device
  assertions; on a machine with the device, that the explicit request is honoured with no
  fallback. These cases carry no category and run in CI.
- **Both branches are exercised locally** by hiding the GPUs from the test process:
  `CUDA_VISIBLE_DEVICES=-1` hides CUDA (and NVIDIA's OpenCL device);
  `GPU_DEVICE_ORDINAL=7` hides AMD's OpenCL device. With both set, `Auto` gives the CPU
  accelerator. Measured 2026-10-03.
- **The `Gpu` cases are specific to the owner's machine**: D1, D2, L5 and L7 name the RTX
  5070 Ti (CUDA) or `gfx1036` (OpenCL), as the checks do. They fail elsewhere by design.
- **D2 opens CUDA through the package's `DeviceSelector` and loads the probe through its
  `KernelLoader`**, so the kernel compiles against libdevice and is completed by the
  post-link, as a run's kernels are. The arguments are a log-spaced grid of 10⁴ points over
  [1e-3, 700]; the distance is counted on the ordered IEEE 754 bit patterns (`Ulp`), whose
  own positive controls use `Math.BitIncrement`/`BitDecrement`.
- **OpenCL is measured, not judged**: D2 names CUDA only, so the OpenCL case asserts
  only that every result is a number and reports the distances.
- **The PTX texts of L2 are ILGPU's and libnvvm's output, not typed**, generated once on
  2026-10-03 with ILGPU 1.5.3 and the libnvvm and `libdevice.10.bc` of CUDA Toolkit 13.4
  (`v13.4\nvvm\bin\x64`), from `MathProbe.Probe`, line ends LF:
  - `Ptx/probe.sm_120.ptx`: `PTXBackend.Compile` of the RTX 5070 Ti's accelerator
    (`SM_120`, ISA 8.8), context with `Math(MathMode.Default)` and `LibDevice`; it calls
    four wrappers and defines none. SHA-256 `854c3b70…02e2e30`.
  - `Ptx/probe.sm_120.linked.ptx`: the same compiled kernel after
    `LibDevicePostLink.Link`. SHA-256 `2a78a084…3afbd9eb`.
  - `Ptx/probe.sm_89.ptx`: a `PTXBackend` built for `CudaArchitecture.SM_89` and
    `CudaInstructionSet.ISA_85`, with libnvvm; ILGPU defined the four wrappers itself.
    SHA-256 `abb55bfc…674e34`.

  ILGPU 1.5.3 writes `.target sm_80` in all three: the files are named by the
  architecture the backend was built for. No fact names a wrapper; each asserts a
  relation the texts stand in.

## Dependencies

- [Devices](../../../src/DotNetDifferentialEvolution.GPU/Devices/API.md) — `DeviceSelector`, `AcceleratorLease`, `KernelLoader`, `MathProbe` (internal).
- [LibDevice](../../../src/DotNetDifferentialEvolution.GPU/Devices/LibDevice/API.md) — `LibDeviceLocator`, `LibDevicePostLink`, `CudaWslDevices` (internal).
- [Objectives](../../../src/DotNetDifferentialEvolution.GPU/Objectives/API.md) — `IGpuFitnessFunction`, `GeneView`.
- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md)
  — the builder, `GpuDeviceInfo`.

Outside the tree: ILGPU 1.5.3 (`Context`, CUDA, OpenCL, `PTXBackend`, `NvvmAPI`,
`CudaAPI`), xUnit 2.9.3; for the `Gpu` cases, the owner's RTX 5070 Ti, `gfx1036` and a CUDA
Toolkit.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- A `Gpu` case is marked on the method; CI filters `Category!=Gpu`.
- `IlgpuPinTests` sets a `DllImportResolver` on a fresh copy of
  `DotNetOptimization.Abstractions.dll` in the temporary directory, never on an assembly
  the tests use: a resolver cannot be removed once set. The copy stays loaded and is not
  deleted.

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
- [x] D2 is green (2026-10-03, RTX 5070 Ti, ILGPU 1.5.3, libdevice of CUDA 13.4 through
      the post-link): largest distance from `System.Math` over the 10⁴ arguments — `Exp` 1
      ULP (x = 0.00248219772137655), `Log` 1 (x = 0.6260184850703607), `Pow(x, 1.37)` 1
      (x = 0.0010006731682568205), `Sqrt` 0; none over 4. OpenCL `gfx1036`: 1, 1, 1, 0.
      Red with the post-link returning the kernel unchanged: CUDA no longer opens.
      ⚠ 2026-10-03: was red with `EnableAlgorithms` (Exp 195, Log 9 430, Pow 24 ULP), now
      green through libdevice; the check and its argument grid unchanged → the package's
      [HISTORY.md](../../../src/DotNetDifferentialEvolution.GPU/HISTORY.md#libdevice-port-2026-10-03).
- [x] L1–L7 and L9 are green and each was red once on its mutation: 2026-10-03, local,
      listed per check in the package's `ACCEPTANCE.md`. L6 ran both branches, the
      no-device one with the GPUs hidden.
- [ ] ⚠ The hosted-runner branch was run here only with the GPUs hidden by environment
      variables, not yet on a hosted runner.

## Taboos

- **No looser ULP tolerance and no narrower argument range** to make D2 green: the 4 ULP
  are APT's measurement for libdevice.
- **No device name made generic** in the `Gpu` cases: the checks name the devices.
- **No PTX text edited by hand**: a new one is generated and its provenance recorded here.
