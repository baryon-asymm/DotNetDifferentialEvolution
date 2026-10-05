# DotNetDifferentialEvolution.GPU

Differential Evolution with the whole population on a GPU. Each generation is one kernel
launch in which every thread builds, evaluates and selects one individual; your objective
is a struct compiled into that kernel by [ILGPU](https://github.com/m4rs-mt/ILGPU/). It
runs on NVIDIA GPUs through CUDA, on other GPUs through OpenCL, and on ILGPU's CPU
accelerator when there is no GPU.

It pays off when the objective is cheap per call and the population is large: thousands
of individuals, one GPU thread each. Its builder has the schemes, variants and stop rules
of the CPU package, by the same names and defaults. For expensive objectives, host-side
code or your own strategies, use the CPU package,
[DotNetDifferentialEvolution](https://www.nuget.org/packages/DotNetDifferentialEvolution).

## Installation

```bash
dotnet add package DotNetDifferentialEvolution.GPU
```

.NET 8 or later. For CUDA, an NVIDIA driver and the
[CUDA Toolkit](https://developer.nvidia.com/cuda-downloads): the package takes `Exp`, `Log` and
`Pow` from its libdevice, found through `CUDA_PATH` or the toolkit's default directories (tested
with 12.9 and 13.4). Without the toolkit, `GpuDevice.Auto` skips CUDA, with the reason. For
OpenCL, a GPU driver whose OpenCL device ILGPU accepts (Auto skips it, with the reason, when
ILGPU does not). ILGPU comes with the package.

## Quick start

```csharp
using DotNetDifferentialEvolution.GPU;
using DotNetDifferentialEvolution.GPU.Objectives;

double[] lowerBound = [-5.0, -5.0, -5.0, -5.0, -5.0];
double[] upperBound = [5.0, 5.0, 5.0, 5.0, 5.0];

using var optimizer = GpuDifferentialEvolutionBuilder
    .ForFunction(new Sphere())
    .WithBounds(lowerBound, upperBound)
    .WithPopulationSize(10_000)
    .WithDefaultMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)
    .WithGenerationLimit(500)
    .OnDevice(GpuDevice.Auto)
    .WithSeed(1)
    .Build();

var result = await optimizer.RunAsync();

Console.WriteLine($"{result.Device.Kind} ({result.Device.Name}): f = {result.FitnessFunctionValue}");
Console.WriteLine(string.Join(", ", result.Genes.ToArray()));

public readonly struct Sphere : IGpuFitnessFunction
{
    public double Evaluate(GeneView genes)
    {
        var sum = 0.0;
        for (var j = 0; j < genes.Length; j++)
        {
            sum += genes[j] * genes[j];
        }

        return sum;
    }
}
```

## Writing the objective

The objective is a **struct** implementing `IGpuFitnessFunction`. `Evaluate` receives a
read-only view of one individual's genes and returns its fitness: lower is better, and
`NaN` ranks worst. Its body runs on the GPU, so it is kernel code:

- value types only; no classes, strings, arrays allocated in the body, exceptions or
  virtual calls;
- `Math.Abs`, `Sqrt`, `Exp`, `Log`, `Pow`, `Floor`, `Min`, `Max` and `double.IsNaN` compile
  on every backend. `Exp`, `Log`, `Pow` and `Sqrt` agree with `System.Math` within 1 unit in
  the last place on CUDA (libdevice) and on OpenCL, measured on 10⁴ arguments in
  [10⁻³, 700]; the CPU accelerator calls `System.Math` itself;
- data the objective needs (fit points, constants) goes in its fields, as value types or
  as ILGPU `ArrayView`s allocated on the same accelerator (pass that accelerator with
  `OnAccelerator`);
- reading `genes[j]` outside `0 ≤ j < genes.Length` is undefined: kernels cannot check.

The type must be **public**, or internal in an assembly that declares
`[assembly: InternalsVisibleTo("ILGPURuntime")]`: ILGPU emits its launchers into a dynamic
assembly of that name. A private nested struct fails at `Build` with "Access is denied".

ILGPU reports code it cannot compile when the optimizer is built, not when C# compiles.

## Devices

- `GpuDevice.Auto` tries CUDA, then OpenCL, then the CPU accelerator.
  `result.Device.FallbackReason` says why it skipped each backend before the one it chose:
  for CUDA, no device or no CUDA Toolkit, for instance.
- `GpuDevice.Cuda`, `OpenCL` or `Cpu` uses that device, or `Build` throws
  `InvalidOperationException` naming it. An explicit device never falls back.
- `OnAccelerator(accelerator)` runs on your own ILGPU accelerator, which the optimizer
  never disposes. On CUDA, build its context with
  `.Math(MathMode.Default).LibDevice(libnvvmPath, libdevicePath)` if the objective calls
  `Exp`, `Log` or `Pow`, and for JADE, SHADE and L-SHADE, whose samplers call `Log`, `Cos`
  and `Tan`.

## The run

- **Stop rules:** `WithGenerationLimit(n)` runs exactly `n` generations;
  `WithEvaluationLimit(m)` stops at the first generation boundary where the evaluation
  count, which starts at N for the initial population, reaches `m`;
  `WithStagnationLimit(streak, threshold)` stops when the best value has moved by no more
  than `threshold` for `streak` generations in a row, as the CPU package's
  `StagnationStreakTerminationStrategy`.
- **Asynchronous:** `RunAsync` returns at once and runs the generations on a thread of its
  own. A cancellation token is observed between generations and ends the task as
  canceled. After a run, calling `RunAsync` again returns the same task.
- **The population stays on the device.** It is copied to the host once at the end, and
  once per observer call if you register one with `WithPopulationUpdateHandler(handler,
  everyNGenerations)`.
- **Reproducible:** the same seed on the same device, with the same package and ILGPU
  versions, gives a bit-identical result. The random numbers (Philox4x32-10, a
  counter-based generator) are identical on every backend, but results across backends
  may differ, because floating-point code generation is the backend's.
- **Result:** the best individual of the final population (`ISolution` from
  `DotNetOptimization.Abstractions`), with the number of generations and evaluations and
  the device it ran on.

## Schemes and variants

The same as the CPU builder's, with its semantics draw for draw:

- fixed F and CR: `WithDefaultMutationStrategy` (rand/1), `WithBestMutationStrategy`,
  `WithCurrentToBestMutationStrategy`, `WithRandTwoMutationStrategy`,
  `WithBestTwoMutationStrategy`;
- adaptive: `WithJde()`, `WithJade()`, `WithShade()`, `WithLShade(maxEvaluationNumber)`
  (with an evaluation limit, it must equal `maxEvaluationNumber`).

Every scheme draws distinct donors, crosses binomially with one guaranteed mutant gene,
repairs out-of-box genes to the midpoint between the bound and the parent, and keeps the
trial when it is at least as good as its parent (JADE: strictly better). Details:
[docs/ALGORITHMS.md](https://github.com/baryon-asymm/DotNetDifferentialEvolution/blob/main/docs/ALGORITHMS.md).

## Version 1.0.0

1.0.0 is a new library under the old name: every public type of 0.x is gone. The
hand-assembled `KernelController`, the strategy structs and `XorShift32` states are
replaced by the builder; the objective returns its value instead of writing into the
population; the result is an `ISolution`; runs are seeded; `RunAsync` no longer blocks;
`Dispose` no longer forces a garbage collection. Besides DE/rand/1/bin it has the CPU
package's other four schemes, jDE, JADE, SHADE, L-SHADE and the stagnation stop rule,
which 0.x had not. Release notes: `CHANGELOG.md` beside this file.

## License

MIT, see
[LICENSE](https://github.com/baryon-asymm/DotNetDifferentialEvolution/blob/main/LICENSE).
Issues and contributions: [the repository](https://github.com/baryon-asymm/DotNetDifferentialEvolution).

The package uses [ILGPU](https://github.com/m4rs-mt/ILGPU/), licensed under the
[University of Illinois/NCSA Open Source License](https://github.com/m4rs-mt/ILGPU/blob/master/LICENSE.txt);
a copy is shipped as `ILGPU_LICENSE`.
