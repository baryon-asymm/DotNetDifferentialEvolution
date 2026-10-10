# ACCEPTANCE.md — DotNetDifferentialEvolution.GPU/Devices

The node's acceptance criteria (AGENTS.md 3.2, §6). Checks 7b, D1 and D2 stay in the
package's [ACCEPTANCE.md](../ACCEPTANCE.md), L5–L8 in [LibDevice](LibDevice/ACCEPTANCE.md).
The checks here follow that file's rules: proven twice (green on a known answer, red once on
the named mutation, applied to a scratch copy and never committed), frozen, CI unless marked
**Gpu**.

## Audit fixes — checks A1–A2, frozen 2026-10-10, before code

From the audits of 2026-10-09 ([HISTORY.md](../HISTORY.md#audit-fixes-decided-2026-10-10)).

- [ ] **A1, no kernel shared between loads** (MEM-1, SUS-1, SUS-2). On the CPU accelerator:
      two `KernelLoader.Load` calls for one kernel method on one accelerator return two
      `Kernel` objects, and disposing the first leaves the second undisposed. End to end,
      two optimizers built with `OnAccelerator` on one CPU accelerator (JADE, Sphere D = 3 in
      [−5, 5], N = 32, 20 generations, seeds 1 and 2) hold no common `Kernel`; after the
      first's `Dispose` the second runs and equals its run alone bit for bit, and a third
      built afterwards (seed 3) equals its run alone. **Gpu**, on OpenCL (`gfx1036`): the
      same; and four optimizers running at once on one accelerator (JADE, Sphere D = 10,
      N = 64, 200 generations, observer every 10, seeds 1–4), ten repeats, each equal to its
      run alone, result and every snapshot. Red: the non-CUDA branch loading through
      `Accelerator.LoadAutoGroupedKernel(MethodInfo)` again — measured 2026-10-09 at
      `c40868e` on `gfx1036`: the second run throws `CLException`, so does the third's
      `Build`, and four concurrent optimizers kill the process in `clSetKernelArg`.
- [ ] **A2, the launch is spread over the device** (PERF-1). `KernelLoader.GroupSize` gives,
      for warp 32, 70 multiprocessors and a limit of 640: extent 1 → 32, 1 024 → 32,
      16 384 → 256, 44 800 → 640, 10⁶ → 640; for warp 64, 12 multiprocessors and a limit of
      256: 1 024 → 128. Every kernel is loaded for the largest extent it is launched with in
      the run; results do not change (P0's hash; under **Gpu** every equality check).
      **Gpu**, CUDA: P4's monolithic objective at N = 1 024 takes at most 8 ms per
      generation (median of three batches of 50 after a warm-up; 29.4 ms at `c40868e`,
      3.75 ms with groups of 32 forced, measured 2026-10-09). Red: the upper clamp removed
      (10⁶ → 14 304) in CI; ILGPU's own group size on CUDA under **Gpu** (about 29 ms).
