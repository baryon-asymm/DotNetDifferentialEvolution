# API.md — DotNetDifferentialEvolution

Tree root. The repository exposes two NuGet packages; everything else in it is a test,
a benchmark or a tool. The consumer guides are `README.md` and `docs/AGENT_GUIDE.md`
(CPU) and `src/DotNetDifferentialEvolution.GPU/README.md` (GPU).

## How the system is used ✅

Both packages minimize a box-bounded objective over real-valued genes. They share one
type, `ISolution` from `DotNetOptimization.Abstractions`, and reference nothing of each
other.

| | CPU package | GPU package |
|---|---|---|
| Entry point | `DifferentialEvolutionBuilder.ForFunction(objective)` → staged calls → `Build()` | `GpuDifferentialEvolutionBuilder.ForFunction(objective)` → staged calls → `Build()` |
| Objective | `IFitnessFunctionEvaluator` (`DotNetOptimization.Abstractions`), host code, called from every worker thread | an `IGpuFitnessFunction` struct compiled into the ILGPU kernel |
| Run | `RunAsync()` / `RunAsync(CancellationToken)` → `Task<Population>`, the final population read through a cursor | `RunAsync(CancellationToken)` → `Task<GpuOptimizationResult>`, the best individual as an `ISolution`, on a thread of its own |
| Algorithms | classic DE (seven schemes), jDE, JADE, SHADE, L-SHADE, custom variants through `IDeVariant` | DE/rand/1/bin |
| Devices | CPU threads | CUDA, OpenCL or ILGPU's CPU accelerator |
| Reproducible | `WithSeed`, per worker count | `WithSeed`, per device and versions |
| Contract | [CPU API](src/DotNetDifferentialEvolution/API.md) | [GPU API](src/DotNetDifferentialEvolution.GPU/API.md) |

## Children

- [DotNetDifferentialEvolution](src/DotNetDifferentialEvolution/API.md) — the CPU
  package.
- [DotNetDifferentialEvolution.GPU](src/DotNetDifferentialEvolution.GPU/API.md) — the
  GPU package.
- [Tests.Common](tests/DotNetDifferentialEvolution.Tests.Common/API.md) — CPU test
  support.
- [protocol-lint](tools/protocol-lint/API.md) — the tree's file-level checks.

## Test nodes

- [UnitTests](tests/DotNetDifferentialEvolution.UnitTests/API.md) — what the CPU
  package may consider proven part by part.
- [IntegrationTests](tests/DotNetDifferentialEvolution.IntegrationTests/API.md) — what
  it may consider proven as a whole.
- [GPU.Test](tests/DotNetDifferentialEvolution.GPU.Test/API.md) — what the GPU package
  may consider proven: its frozen checks, on ILGPU's CPU accelerator in CI and on CUDA
  and OpenCL locally.
- [Benchmark](benchmarks/DotNetDifferentialEvolution.Benchmark/API.md) — measurement
  only; asserts nothing.
- [Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/API.md) — what the
  tree may consider machine-checked about its documents against the compiled code.
