# Changelog — DotNetDifferentialEvolution.GPU

Notable changes to the GPU package, released from `gpu-v*` tags. Versions follow
[semantic versioning](https://semver.org/). The release workflow takes the notes of a version
from its `## <version>` section below.

## 1.0.0

A new library under the old name: every public type of 0.x (0.1.0, 0.0.2 and 0.2.0) is gone.

- **The builder** replaces the hand-assembled `KernelController`, the strategy structs and the
  `XorShift32` states: `GpuDifferentialEvolutionBuilder.ForFunction(objective)`, then bounds,
  population size, scheme, stop rule and device.
- **The CPU package's schemes and variants**, by its names, parameters and defaults, with its
  semantics draw for draw: `WithDefaultMutationStrategy` (rand/1), `WithBestMutationStrategy`,
  `WithCurrentToBestMutationStrategy`, `WithRandTwoMutationStrategy`,
  `WithBestTwoMutationStrategy`, `WithJde`, `WithJade`, `WithShade`, `WithLShade`.
- **Stop rules:** `WithGenerationLimit`, `WithEvaluationLimit`, `WithStagnationLimit`.
- **The objective** is a struct implementing `IGpuFitnessFunction`, compiled into the kernel; it
  returns its value instead of writing into the population.
- **Runs are seeded** (Philox4x32-10, the same draws on every backend) and reproducible on one
  device; `RunAsync` no longer blocks and observes a cancellation token.
- **The result** is an `ISolution` (`DotNetOptimization.Abstractions`).
- **Devices:** CUDA (math through libdevice; a CUDA Toolkit is needed), OpenCL, or ILGPU's CPU
  accelerator; `Auto` falls back and says why. ILGPU is pinned to 1.5.3.
- `Dispose` no longer forces a garbage collection.
