# API.md — DotNetDifferentialEvolution

Tree root. The repository exposes two NuGet packages; everything else in it is a test,
a benchmark or a tool. The consumer guides are `README.md` and `docs/AGENT_GUIDE.md`
(CPU) and `src/DotNetDifferentialEvolution.GPU/README.md` (GPU).

## How the system is used ✅

Both packages minimize a box-bounded objective over real-valued genes. They share no
type.

| | CPU package | GPU package |
|---|---|---|
| Entry point | `DifferentialEvolutionBuilder.ForFunction(objective)` → staged calls → `Build()` | `new DifferentialEvolutionOptimizer(kernelController)`, the strategies being struct type arguments of `KernelController<…>`, built by hand |
| Objective | `IFitnessFunctionEvaluator` (`DotNetOptimization.Abstractions`), host code, called from every worker thread | an `IFitnessFunctionInvoker` struct compiled into the ILGPU kernel |
| Run | `RunAsync()` / `RunAsync(CancellationToken)` → `Task<Population>`, the final population read through a cursor | `RunAsync()` / `RunAsync(CancellationToken)` → `Task<OptimizationResult>` (the best individual), run synchronously on the caller's thread |
| Algorithms | classic DE (seven schemes), jDE, JADE, SHADE, L-SHADE, custom variants through `IDeVariant` | classic DE |
| Reproducible | `WithSeed`, per worker count | no seed |
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
- [GPU.Test](tests/DotNetDifferentialEvolution.GPU.Test/API.md) — two end-to-end GPU
  runs against known optima; local only (needs OpenCL).
- [Benchmark](benchmarks/DotNetDifferentialEvolution.Benchmark/API.md) — measurement
  only; asserts nothing.
- [Protocol.Tests](tests/DotNetDifferentialEvolution.Protocol.Tests/API.md) — what the
  tree may consider machine-checked about its documents against the compiled code.
