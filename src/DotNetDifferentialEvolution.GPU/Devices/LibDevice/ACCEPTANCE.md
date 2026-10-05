# ACCEPTANCE.md — DotNetDifferentialEvolution.GPU/Devices/LibDevice

The node's acceptance criteria (AGENTS.md 3.2, §6): checks L1–L9 of CUDA math through
libdevice. Written 2026-10-03 in the package's [ACCEPTANCE.md](../../ACCEPTANCE.md), before
the port's code, under the rules stated there (proven twice, frozen, where they run);
moved here unchanged on 2026-10-05, when the package's file reached its limit (§15).

## CUDA math through libdevice — checks added 2026-10-03, before the port's code ✅

⚠ 2026-10-03: added on the owner's decision to resolve D2 as APThermo does: libdevice
completed by a post-link of the package's own, in place of ILGPU.Algorithms →
[HISTORY.md](../../HISTORY.md#libdevice-port-2026-10-03). D1, D2 and 7b stay as written; only the contexts
they open change.

- [x] **L1, discovery** (CI). Over fake toolkit trees in a temporary directory, through the
      locator's seam (platform, environment and base directory given, not read):
      - Windows: `CUDA_PATH` before the versioned directories; `v13.3` before `v9.0` (by
        parsed version, not by string); both the `nvvm\bin` and the `nvvm\bin\x64` layout; a
        root with the library and no bitcode passed over for the next; a root named twice
        tried once; no `CUDA_PATH` and no base directory → nothing tried.
      - Linux: `CUDA_PATH`, then `CUDA_HOME`, then `<base>/cuda`, then `cuda-*` newest
        first; a root named twice tried once.
      - Any other platform: nothing tried.
      Red: the versions sorted as strings; the bitcode check removed.
      2026-10-03: `Devices/LibDeviceDiscoveryTests`, 12 cases, green. Red: the versions sorted
      as strings (`WindowsOrdersTheVersionedDirectoriesNewestFirst`,
      `LinuxOrdersTheVersionedDirectoriesNewestFirst`); the bitcode check removed
      (`ALibraryWithoutBitcodeIsPassedOverForTheNextRoot` and both "a root named twice" cases).
- [x] **L2, wrapper inventory** (CI). Over two committed PTX texts of `MathProbe.Probe`,
      their provenance recorded beside them: ILGPU 1.5.3's own for `sm_120` and the same
      after the post-link. The called set is the same in both and not empty; the first
      defines none of it; in the second, called = defined; no parameter name is read as a
      call; LF and CRLF give the same sets. Red: the call pattern without its trailing
      comma.
      ⚠ 2026-10-03, changed before it was ticked: **a third text, and another red mutation.** The
      named mutation, the call pattern without its trailing comma, stayed green, on APT's
      regular expression and on the hand-written reading alike: in every PTX text ILGPU 1.5.3
      and libnvvm emit here, the callee is the first wrapper name after `call` in its
      statement, so the comma decides nothing. What keeps a definition's parameter
      (`__ilgpu__nv_pow_param_0,` on a line of its own) from being read as a call is the `call`
      anchor. Now the red mutation is the anchor removed, and the texts are three: ILGPU's own
      PTX for `SM_89`, where ILGPU defines the wrappers itself, is added (provenance in
      `tests/…/Devices/BOOT.md`). Nothing loosened: the five relations of the check hold on all
      three texts, plus "below compute 10.0 ILGPU defines every wrapper it calls".
      2026-10-03: `Devices/WrapperInventoryTests`, 6 facts, green. Red with the `call` anchor
      removed: 4 of 6 (`NoParameterNameIsReadAsACall`, `TheLinkedPtxDefinesEveryWrapperItCalls`,
      `BelowCompute10IlgpuDefinesEveryWrapperItCalls`, `AllThreeTextsCallTheSameWrappers`).
      The trailing comma made optional: green, as above.
- [x] **L3, post-link guards** (CI). The check after compilation names exactly the wrappers
      left without a definition and is not fooled by a call site. Every non-success
      `NvvmResult` and `CudaError`, read from the enums, throws naming the post-link, the
      library, the call, the result and the target; a log is carried, trimmed of NUL
      padding, and a log that trims to nothing leaves no trailing ": ". Red: the
      definition check run over the whole linked text; the trim without `'\0'`.
      2026-10-03: `Devices/PostLinkGuardTests`, 73 cases (every `NvvmResult` and `CudaError`
      but success), green. Red: the definition check run as a substring search of the body
      (`ACallSiteIsNotADefinition`); the trim without NUL (`ALogIsCarriedWithoutItsPadding`,
      `ALogThatTrimsToNothingIsNoLog`).
- [x] **L4, the ILGPU pin** (CI). The assertion of the ILGPU version and of the reflected
      members passes on the referenced ILGPU and, for another expected version, throws
      naming both. The WSL workaround's reflection names a member it cannot find. Red: the
      version comparison removed.
      2026-10-03: `Devices/IlgpuPinTests`, the first three facts, green. Red with the version
      comparison removed: `AnotherExpectedVersionThrowsNamingBoth`.
- [x] **L5, the post-link on the device** (**Gpu**). On the RTX 5070 Ti the probe kernel's
      own PTX calls wrappers and defines none; `Link` compiles exactly those, and the
      result loads. CUDA counts as opened only after the probe kernel has loaded through
      the post-link. Red: `Link` returning the kernel unchanged → `Open(Cuda)` throws with
      the driver's result.
      2026-10-03: `Devices/CudaLibDeviceTests.OnTheRtx5070TiThePostLinkCompletesExactlyTheMissingWrappers`,
      green with the toolkits 13.4 and 12.9: ILGPU's PTX calls `__nv_exp`, `__nv_log`,
      `__nv_pow`, `__nv_sqrt` and defines none; `Link` compiles exactly those four and the
      kernel loads. Red with `Link` returning the kernel unchanged: `Open(Cuda)` throws "The
      CUDA device was requested and cannot be used: the math probe kernel could not be loaded:
      a PTX JIT compilation failed; releasing it also failed: invalid resource handle", and
      D2, L7 and L9's device case fail the same way. The first run of this mutation found that
      ILGPU's accelerator throws from `Dispose` after its loader failed a kernel, and that
      exception escaped `Open`; fixed before this tick (`Devices/LibDevice/BOOT.md`, the
      post-link, stage 3).
- [x] **L6, no toolkit** (CI, conditional like D1). With the locator finding nothing, on a
      machine with a CUDA device: an explicit `Cuda` throws naming CUDA, libnvvm and
      libdevice; `Auto` skips CUDA with that reason. With no CUDA device the reason is the
      missing device, as in D1. Red: CUDA opened without libdevice.
      2026-10-03: `Devices/CudaLibDeviceTests.WithoutAToolkitCudaIsRefusedWithTheReason`, green;
      the device branch, here: "The CUDA device was requested and cannot be used: libnvvm
      (nvvm64_40_0.dll) and libdevice (libdevice.10.bc) of a CUDA Toolkit were not found; there
      was no CUDA_PATH and no toolkit directory to look in.", and Auto gave OpenCL with that
      reason. Red with CUDA opened without libdevice (the "not found" branch removed): the
      probe then fails to compile and the message names neither file. The no-device
      branch, with the GPUs hidden (`CUDA_VISIBLE_DEVICES=-1`, `GPU_DEVICE_ORDINAL=7`): "The
      CUDA device was requested and cannot be used: no such device is present.", and Auto
      gave `Cpu` with "CUDA: no such device is present.; OpenCL: no such device is
      present." Not yet run on a hosted runner.
- [x] **L7, a bad library** (**Gpu**). A file named as libnvvm that is not a library, with
      the real bitcode: an explicit `Cuda` throws naming its path; 20 `Auto` opens fall
      back and cost at most 64 MiB of free device memory (APT's bound for the same check).
      Red: the library checked only after the accelerator is created.
      2026-10-03: `Devices/CudaLibDeviceTests.ABadLibraryIsNamedAndNeverReachesTheDevice`, green:
      "libnvvm (…\nvvm64_40_0.dll) or libdevice (…\libdevice.10.bc) could not be loaded: An
      attempt was made to load a program with an incorrect format. (0x8007000B)"; free device
      memory 15 037 MiB before and after 20 failed opens. Red with the library checked after the
      accelerator is created.
- [x] **L8, no ILGPU.Algorithms** (CI, Protocol.Tests). The GPU assembly references no
      `ILGPU.Algorithms`. Red: the reference and `EnableAlgorithms()` restored.
      2026-10-03: `Protocol.Tests/GpuGuardTests.TheGpuPackageUsesNoIlgpuAlgorithms`, green. Red with
      the package reference and `EnableAlgorithms()` restored.
- [x] **L9, every CUDA context of a process binds** (CI and **Gpu**). The WSL resolver
      failure is recognised by where it was thrown, not by its message. Under **Gpu**,
      three CUDA optimizers built one after another each bind. Red: recognition by
      message.

      2026-10-03: `Devices/IlgpuPinTests.TheResolverFailureIsRecognisedByWhereItWasThrownNotByItsMessage`,
      green; under **Gpu**, `Devices/CudaLibDeviceTests.EveryCudaOptimizerOfTheProcessBinds`, green
      on Windows. Red with recognition by message. Not yet run under WSL, where the workaround's
      own branch runs.
