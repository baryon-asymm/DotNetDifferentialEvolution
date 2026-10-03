# API.md — DotNetDifferentialEvolution.GPU.Test

The node exposes nothing outward: nobody references a test project. Its contract points
upward: it is what the GPU package may consider proven, check by check of the package's
[ACCEPTANCE.md](../../src/DotNetDifferentialEvolution.GPU/ACCEPTANCE.md).

## What this node guarantees

| Claim | Checks | Child |
|---|---|---|
| Philox4x32-10 matches Random123's known answers, on the host and in a kernel on every backend | 3a | [Random](Random/API.md) |
| Uniform doubles are uniform and below 1; index draws are Lemire's multiply-shift | 3b, 3c | [Random](Random/API.md) |
| The draws of a (seed, individual, generation) are the same words on every backend | 4b | [Random](Random/API.md) |
| One DE step: donors distinct and uniform, crossover with `jrand`, midpoint repair, survival with `NaN`, best pick | 1b–1f | [Kernels](Kernels/API.md) |
| The GPU step and the CPU package's step build bit-identical trials from the same draws | 1g | [Kernels](Kernels/API.md) |
| A generation writes slot i from parent i or trial i only; the objective's view cannot write | 2b, 2a | [Kernels](Kernels/API.md), [Objectives](Objectives/API.md) |
| Every argument error of the package's API is raised where documented | B1 | [Builder](Builder/API.md) |
| The device choice, the fallback reason and the math probe | D1, D2 | [Devices](Devices/API.md) |
| Initial sampling, convergence to known optima, reproducibility, one download per run, asynchrony, cancellation, ownership | 1a, 1h, 4a, 5b, 6a–6c, 7b | [EndToEnd](EndToEnd/API.md) |

Claims do not scale up the ladder: a green whole run over a red step would mean "converges
for an unknown reason", not "correct".

## Children

- [Random](Random/API.md) — the RNG checks and the χ² helper.
- [Kernels](Kernels/API.md) — the DE step's checks, scripted draws, the CPU parity.
- [Objectives](Objectives/API.md) — the gene view's surface.
- [Builder](Builder/API.md) — the argument errors.
- [Devices](Devices/API.md) — device selection and the math probe.
- [EndToEnd](EndToEnd/API.md) — whole runs.
