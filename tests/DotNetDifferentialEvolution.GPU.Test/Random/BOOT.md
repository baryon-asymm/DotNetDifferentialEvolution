# BOOT.md — GPU.Test/Random

## Purpose

The GPU package's random numbers, checked against sources that share no code with them:
ACCEPTANCE.md checks **3a** (Philox4x32-10 known answers), **3b** (uniform doubles),
**3c** (index draws) and **4b** (the same draws on every backend). Also home of the χ²
helper that check 1b ([Kernels](../Kernels/API.md)) uses.

| Check | Test | Against what |
|---|---|---|
| 3a | `PhiloxKnownAnswerTests` (host, CPU accelerator); `DeviceDrawTests` (CUDA, OpenCL) | Random123 `tests/kat_vectors`, lines 27–29 |
| 3b | `UnitDoubleTests` | χ² quantile computed by `ChiSquared`; the exact value `1 − 2⁻⁵³` |
| 3c | `IndexDrawTests` | `⌊word·n / 2³²⌋` in `BigInteger`; hand-computed values |
| 4b | `DrawSequenceTests` (CPU accelerator); `DeviceDrawTests` (CUDA, OpenCL) | the host's `PhiloxDraws.NextUInt()` words |
| — | `ChiSquaredTests` | closed forms: df = 2, `1 − e^(−x/2)`; even df, the Poisson tail |

## Invariants

- **The known answers are cited, not produced.** Random123 at
  `9545ff6413f258be2f04c1d319d99aaef7521150`, `tests/kat_vectors` (SHA-256
  `aab5ebab…86929183`), lines 27–29: the zero, all-ones and π-digit vectors. Held by
  `PhiloxKatVectors`, whose doc comment carries the full citation.
- **No quantile is typed from memory.** `ChiSquared.Quantile` bisects the regularized
  incomplete gamma function; `ln Γ(df/2)` is exact (integer or half-integer argument),
  not an approximation. Its controls are closed forms with none of its code.
- **Draw comparisons are bit for bit** (`Assert.Equal` on `uint[]`). Results across
  backends are not compared, only the words (package `BOOT.md`, invariant 4).
- **Device tests are their own classes.** `DeviceDrawTests` carries only
  `Category=Gpu`; a Gpu test inside a `Unit` or `Integration` class would also match
  CI's `Category=Unit` filter.

## Dependencies

- [Kernels](../../../src/DotNetDifferentialEvolution.GPU/Kernels/API.md) — the DE step and the kernels (internal).
- [Random](../../../src/DotNetDifferentialEvolution.GPU/Random/API.md) — Philox4x32-10, the draw sources and conversions (internal).
- [DotNetDifferentialEvolution.GPU](../../../src/DotNetDifferentialEvolution.GPU/API.md) —
  internal `Philox4x32x10`, `PhiloxBlock`, `PhiloxDraws`, `DrawConversions`,
  `GpuKernels.PhiloxBlocks`, `GpuKernels.DrawSequence`, `StepParameters`; public
  `GpuDevice` (InternalsVisibleTo).

Outside the tree: ILGPU 1.5.3 (`Context`, CPU, CUDA and OpenCL
accelerators), xUnit.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- `Unit` for host-only tests, `Integration` for a kernel on ILGPU's CPU accelerator,
  `Gpu` for CUDA and OpenCL; the last run locally only (RTX 5070 Ti, `gfx1036`).
- Host↔device copies only through the array overloads `CopyFromCPU(T[])` and
  `CopyToCPU(T[])`.
- No `System.Random` (CA5394): every stream is a seeded `PhiloxDraws`.

## Acceptance criteria

- [x] 3a green: 2026-10-03, `PhiloxKnownAnswerTests` 4 of 4 (host × 3, CPU-accelerator
      kernel) and `DeviceDrawTests.AKernelOnTheDeviceGivesTheKnownAnswers` on CUDA and
      OpenCL, local.
- [x] 3a red: 2026-10-03, scratch mutation `Multiplier0` `0xD2511F53` → `0xD2511F57`:
      all six known-answer cases fail (`PhiloxKnownAnswerTests.cs` lines 19 and 30,
      `DeviceDrawTests.cs` line 24).
- [x] 3b green: 2026-10-03, `UnitDoubleTests` 2 of 2; 10⁶ draws, 100 bins, χ² under
      the computed 0.999 quantile, df = 99.
- [x] 3b red: 2026-10-03, scratch mutation `ToUnitDouble` → `high · 2⁻³²`: the
      analytic maximum fails (`UnitDoubleTests.cs` line 49: 0.99999999976716936
      against 0.99999999999999989); the χ² test alone stays green, as expected.
- [x] 3c green and red: 2026-10-03, `IndexDrawTests` 2 of 2; scratch mutation
      `word % n` fails both (lines 33 and 43).
- [x] 4b green: 2026-10-03, `DrawSequenceTests` 2 of 2 (10⁴ words, CPU accelerator
      against host; a different seed, individual or generation differs), and
      `DeviceDrawTests.TheDeviceDrawsTheHostWords` on CUDA and OpenCL, local. No red
      mutation is named for 4b.
- [x] The χ² helper is controlled: 2026-10-03, `ChiSquaredTests` 8 of 8 (df = 2
      quantile equals `−2 ln 0.001` to 1e-12 relative; df 4, 8, 48, 98, 100, 2400 against
      the Poisson tail to 1e-10).

## Taboos

- **No quantile, known answer or expected word typed without its source.**
- **No Gpu test in a class that carries another category.**
- **No comparison of results across backends**, only of the draws.
