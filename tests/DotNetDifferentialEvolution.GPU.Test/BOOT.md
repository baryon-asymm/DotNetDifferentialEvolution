# BOOT.md — DotNetDifferentialEvolution.GPU.Test

## Purpose

What "the GPU package is ready" means today: the levels of verification, what each is
checked against, and what is not covered. As the code stands there is one level, two
end-to-end runs on a real OpenCL device against objectives with known optima.

| Level | What it checks | Against what (source of truth) | State |
|---|---|---|---|
| L0 | single strategies on the device (donor rule, crossover, bound repair, selection ties and `NaN`) | — | absent |
| L1 | the controller's lifecycle, cancellation, observer calls, a second run | — | absent |
| L2 | a full run converges: Rosenbrock 2-D; 6-coefficient polynomial least squares | the analytic minimum; the exact least-squares solution (NumPy, see [FitnessFunctions](FitnessFunctions/BOOT.md)) | ✅ local only |
| Protocol | tree invariant, documents against code | `AGENTS.md`; `tools/protocol-lint` (textual); no reflection checks yet | partial |

A green L2 over absent L0 and L1 means "converges on these two problems", not "each
part is correct".

## Invariants

- **The references are independent of the optimizer.** The analytic Rosenbrock
  minimum, and a polynomial optimum checked against an exact solver on 2026-10-02.
- **Both runs use the package's defaults:** population 10 000, 1 000 generations,
  `MutationStrategy` with F 0.3 and CR 0.8, `SelectionStrategy`,
  `MaxGenerationStrategy`, box ±2000 in every gene. Held by `GetOptimizer` in
  `DifferentialEvolutionOptimizerTests.cs`.
- **The tests carry `Category=Gpu`, and CI excludes that category.** Held by the
  `[Trait]` on the test class and the filter in `.github/workflows/ci.yml`; a new GPU
  test class without the mark would run on hosted runners, which have no OpenCL device.
  What it would do there was not observed, so nothing checks the mark.

## Dependencies

- [DotNetDifferentialEvolution.GPU](../../src/DotNetDifferentialEvolution.GPU/API.md) —
  `DifferentialEvolutionOptimizer`.
- [Controllers/Kernels](../../src/DotNetDifferentialEvolution.GPU/Controllers/Kernels/API.md)
  — `KernelController<…>`.
- [Interfaces](../../src/DotNetDifferentialEvolution.GPU/Interfaces/API.md) —
  `IFitnessFunctionInvoker`, the constraint of `GetOptimizer`.
- [Models](../../src/DotNetDifferentialEvolution.GPU/Models/API.md) —
  `OptimizationResult`, read through `RunAsync`'s result.
- [MutationStrategies](../../src/DotNetDifferentialEvolution.GPU/MutationStrategies/API.md)
  — `MutationStrategy<RandomGenerator>`.
- [PopulationSamplingMakers](../../src/DotNetDifferentialEvolution.GPU/PopulationSamplingMakers/API.md)
  — `PopulationSamplingMaker`.
- [RandomGenerators](../../src/DotNetDifferentialEvolution.GPU/RandomGenerators/API.md)
  — `RandomGenerator`.
- [SelectionStrategies](../../src/DotNetDifferentialEvolution.GPU/SelectionStrategies/API.md)
  — `SelectionStrategy`.
- [TerminationStrategies](../../src/DotNetDifferentialEvolution.GPU/TerminationStrategies/API.md)
  — `MaxGenerationStrategy`.
- [Controllers/Kernels/Interfaces](../../src/DotNetDifferentialEvolution.GPU/Controllers/Kernels/Interfaces/API.md) — `IKernelController`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [PopulationSamplingMakers/Interfaces](../../src/DotNetDifferentialEvolution.GPU/PopulationSamplingMakers/Interfaces/API.md) — `IPopulationSamplingMaker`. Added 2026-10-03 from the reflection check (`DependencyTests`).
- [TerminationStrategies/Interfaces](../../src/DotNetDifferentialEvolution.GPU/TerminationStrategies/Interfaces/API.md) — `ITerminationStrategy`. Added 2026-10-03 from the reflection check (`DependencyTests`).

Outside the tree: xUnit 2.5.3, Microsoft.NET.Test.Sdk 17.8.0, coverlet 6.0.0; ILGPU
and ILGPU.Algorithms 1.5.1 (`Context`, OpenCL, `XorShift32`); an OpenCL device.

## Constraints

Inherited from the root ([BOOT.md](../../BOOT.md)). In addition:

- Settings come from `tests/Directory.Build.props`; the csproj removes its global
  `using DotNetOptimization.Abstractions`, which this package does not reference.
- The context is created with OpenCL only; no CUDA and no CPU accelerator.
- The run time is that of the device: on `gfx1036` about 1 s and 6 s per test.

## Acceptance criteria

- [x] L2 is green and stable: 2026-10-02, `TestRosenbrockCase` and
      `TestPolynomialApproximationFunctionCase`, 5 of 5 consecutive runs each, local,
      OpenCL `gfx1036` (AMD integrated graphics), about 1 s and 6 s per run.
- [x] L2 is non-degenerate: 2026-10-02, in a scratch clone of `63d3ff1`. Selection
      keeping the worse individual (`<` turned into `>`) made both tests fail; crossover
      never taking the mutant (`<= CR` turned into `<= CR - 1`) made both fail; the
      unmutated clone passed both.
- [ ] L0 and L1 do not exist (see the table).
- [ ] The tests run in no CI: hosted runners have no OpenCL device.
- [ ] ⚠ Runs are not seeded: the population comes from `Random.Shared` and the random
      states from `(uint)Random.Shared.Next()`. A failure cannot be reproduced.
- [ ] ⚠ A random state seeded with 0 terminates the test process (ILGPU's assertion).
      Estimate, not measured: 10 000 states per run, each 0 with probability 1 in
      2 147 483 647, about 4.7e-6 per run.
- [ ] ⚠ The tolerances (1e-6 and 1e-8) are local constants in the test bodies.
- [ ] ⚠ The bound and random-state device buffers allocated in `GetOptimizer` are never
      disposed by the test.
- [ ] ⚠ The project is named `.GPU.Test`; its siblings are `.UnitTests`,
      `.IntegrationTests` and `.Tests.Common`.

## Taboos

- **No looser tolerance for the sake of green.** The polynomial's 1e-8 already leaves
  only about 7.8e-9 around the exact optimum; loosening it hides a slower search.
- **No expected value taken from an optimizer run.**
- **No GPU test class without `Category=Gpu`.** CI would run it on runners that have
  no OpenCL device.
