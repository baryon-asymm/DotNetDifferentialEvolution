# BOOT.md — DotNetDifferentialEvolution.GPU/Devices/LibDevice

## Purpose

CUDA math from libdevice, as APThermo has it (`AerospacePropellantThermodynamics`, commit
`5fdd82c`, `src/Execution/LibDevice`; decided 2026-10-03 →
[HISTORY.md](../../HISTORY.md#libdevice-port-2026-10-03)). Four files, the only ones in the
package that know libnvvm, libdevice, PTX and ILGPU's CUDA internals:

- `LibDeviceLocator` finds libnvvm and `libdevice.10.bc` of an installed CUDA Toolkit.
- `LibDevicePostLink` completes a compiled CUDA kernel with the libdevice wrappers it calls
  and ILGPU did not define, and trial-loads the result.
- `PtxText` reads the wrapper calls, the wrapper definitions and the target of a PTX text
  for the post-link.
- `CudaWslDevices` registers the CUDA devices of every context of a process under WSL,
  where ILGPU 1.5.3 binds only the first.

The parent reaches them through `LibDeviceLocator.Locate`, `LibDevicePostLink.Link` and
`.AssertIlgpu`, and `CudaWslDevices.Register` ([API.md](API.md)). Their reason to change
is the versions of ILGPU, libnvvm and the CUDA driver, which the rest of the package does
not share.

## Invariants

- **No libnvvm or driver result is ignored.** Every call is checked through
  `ThrowIfFailed`, the one place that turns a non-success result into an exception naming
  the post-link, the target, the library, the call and the result, with the log where one
  exists. `DestroyProgram` is checked only when the path before it succeeded, so that its
  result never replaces an exception already in flight. Held by check L3.
- **Only the missing wrappers are compiled, and each must come back defined.** Held by
  checks L2, L3 and L5.
- **ILGPU's internals are asserted before use**, once per process: the assembly version
  `1.5.3.0` and the type of each reflected member. A mismatch names the version. Held by
  check L4.
- **No state between calls.** The statics are constants and the `Lazy` reflected members;
  nothing records a discovery, a link or a registration.
- **Nothing here runs on the OpenCL or CPU path.** The parent calls these files only when
  it tries CUDA or loads a kernel on a `CudaAccelerator`.

## Dependencies

None.

Outside the tree: ILGPU 1.5.3 (`ILGPU`, `ILGPU.Backends.PTX`, `ILGPU.Runtime`,
`ILGPU.Runtime.Cuda`); libnvvm and the CUDA driver through ILGPU's `NvvmAPI` and `CudaAPI`;
`libdevice.10.bc` of a CUDA Toolkit. Measured with the toolkits 12.9 and 13.4 on the RTX
5070 Ti (compute 12.0), 2026-10-03.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- **Discovery order.** On **Windows**: `CUDA_PATH`, then
  `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` from the newest version down (by
  parsed version); in each root `nvvm\bin\nvvm64_40_0.dll` (12.x) and
  `nvvm\bin\x64\nvvm64_40_0.dll` (13.x), with `nvvm\libdevice\libdevice.10.bc`. On
  **Linux**: `CUDA_PATH`, `CUDA_HOME`, `/usr/local/cuda`, then `/usr/local/cuda-*` from
  the newest down; in each root `nvvm/lib64/libnvvm.so`, with the same bitcode path. A
  root already tried is skipped; a root whose library exists and whose bitcode does not is
  passed over. Any other platform tries nothing. APT's explicit path pair is not taken: the
  package has no options object, and a caller-owned accelerator brings its own context.
- **The post-link**, in order:
  1. The parent compiles the entry point with the accelerator's own `PTXBackend`.
  2. The wrappers *called* are the `__ilgpu__nv_*` names at `call` instructions only (the
     callee is followed by a comma; a parameter name or a `.func` header is not); the
     wrappers *defined* are the kernel's own `.func` headers. Both drop the `__ilgpu`
     prefix, as ILGPU's fragment keys do.
  3. Nothing missing (no wrapper called, or ILGPU defined every one): nothing is
     compiled, and the kernel is trial-loaded.

     ⚠ 2026-10-03: APT returns a kernel that calls no wrapper untouched, without a trial
     load; here it is trial-loaded too. ILGPU 1.5.3's CUDA accelerator throws "invalid
     resource handle" from `Dispose` after its own loader failed a kernel (measured: the
     probe with its wrappers left undefined), so no PTX reaches that loader before the
     driver has accepted it once. Shown by check L5's mutation, which made `Open(Cuda)`
     escape with the `Dispose`'s exception before the parent appended release failures to
     the reason (the parent's `BOOT.md`).
  4. Otherwise an NVVM module is built from ILGPU's own fragments of the missing wrappers
     (the private static `fragments` of `ILGPU.Backends.PTX.PTXLibDeviceNvvm`), its header
     in the order libnvvm accepts: `target triple`, `target datalayout`, `!nvvmir.version`.
  5. It is compiled with the `NvvmAPI` of the accelerator's backend, which ILGPU loaded
     from the context's `LibDevice` paths, for the `compute_XX` of the kernel's
     `.target sm_XX`. A context without `LibDevice` has none, and the post-link then
     throws naming the missing wrappers.
  6. `.version`, `.target` and `.address_size` are stripped from the result, which is
     inserted right after the kernel's `.address_size` line.
  7. Every missing wrapper must now have a definition in the inserted text.
  8. The result is loaded once through the CUDA driver on the accelerator's bound context,
     as a trial, so that a refusal carries the driver's log.
  9. The private backing field of `PTXCompiledKernel.PTXAssembly` is set by reflection; the
     parent loads the kernel with `LoadAutoGroupedKernel`.
- **Every CUDA context of a process binds under WSL.** ILGPU 1.5.3's `builder.Cuda()` sets a
  `DllImportResolver` on its own assembly whenever it runs under WSL, and .NET allows one,
  so the second context of a process throws before any device is registered.
  `CudaWslDevices.Register` tries the public call every time; when it throws that failure
  (recognised by its `TargetSite`, `NativeLibrary.SetDllImportResolver`, not by its message,
  which a trimmed application replaces with a resource key), it registers the devices
  through ILGPU's internal `CudaDevice.GetDevices(configure, predicate,
  builder.DeviceRegistry)`, the call ILGPU makes right after the resolver (ILGPU source,
  `CudaContextExtensions.cs`, tag `v1.5.3`). Held by checks L4 and L9; not yet run under WSL
  here.
- A libnvvm or driver log is trimmed of NUL padding as well as white space.
- **PTX is read by hand, not by regular expressions.** `PtxText` has the semantics of APT's
  three patterns (its summary quotes them). The analyzers ask for `[GeneratedRegex]`
  (SYSLIB1045, a warning under the tree's settings), and its generated types live outside
  the tree's namespaces, where the protocol's reflection facts refuse them; it also hides
  the partial methods from the source compilation of check 8c. Measured 2026-10-03: with
  `[GeneratedRegex]` three facts of Protocol.Tests were red; with `PtxText`, none.

## Acceptance criteria

→ [ACCEPTANCE.md](ACCEPTANCE.md)

## Taboos

- **No public type here.**
- **No result of a libnvvm or driver call ignored.**
- **No reliance on ILGPU's own wrapper generation**, and no `LibDevice.*`, `XMath` or
  ILGPU.Algorithms (check L8).
