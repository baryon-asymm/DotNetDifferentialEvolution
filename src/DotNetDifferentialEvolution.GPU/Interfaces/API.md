# API.md — GPU Interfaces

Namespace: `DotNetDifferentialEvolution.GPU.Interfaces`. The three contracts a user of
the GPU package implements or holds: the optimizer, the objective that runs inside the
kernel, and the observer of a run. Everything not listed here is internal structure and
may change.

## Optimizer ✅

```csharp
public interface IDifferentialEvolutionOptimizer<T>
{
    Task<T> RunAsync();
    Task<T> RunAsync(CancellationToken cancellationToken);
}
```

`T` is the result type; the package's one implementation returns `OptimizationResult`
([Models](../Models/API.md)). The contract does not promise asynchrony: the
implementation in the package root runs synchronously and returns a completed task.

## Objective inside the kernel ✅

```csharp
public interface IFitnessFunctionInvoker
{
    void Invoke(int individualIndex, DevicePopulation devicePopulation);
}
```

Implemented by a **struct** (the kernel controller constrains it to `struct`) and
called on the device, once per individual per kernel launch, one GPU thread each.
`Invoke` must read the genes `devicePopulation.Individuals[individualIndex, j]` and
write the fitness to `devicePopulation.FitnessFunctionValues[individualIndex]`; lower is
better. It must touch no other individual. Its body is compiled by ILGPU, so it may use
only what a kernel may: value types, `ArrayView`s it carries as fields, `XMath`/`Math`,
loops; no classes, virtual calls, managed allocation, exceptions or recursion.

## Observer of a run ✅

```csharp
public enum OptimizerState
{
    Starting = 0,
    Running,
    Terminating
}

public interface IUpdateOptimizerHandler
{
    void Handle(OptimizerState state, Accelerator device, int generation,
        HostPopulation population);
}
```

Called on the host, synchronously, on the thread that runs the optimization:
`Starting` once with generation 0 before the first generation, `Running` after every
generation with the generation number (from 1) and the population that just became
current, `Terminating` once at the end with the last generation number. The population
lives on the device; reading it means copying it to the host, which the handler pays
for on every call.

## Errors

| Situation | Behaviour |
|---|---|
| `Invoke` uses something ILGPU cannot compile | The kernel fails to load when the controller compiles it, in the optimizer's constructor, with ILGPU's exception |
| `Invoke` does not write its fitness slot | Not detected; the slot keeps whatever value it held before |
| `Handle` throws | The exception propagates out of the run; device memory stays allocated until disposal |

## Side effects

None of their own: these are contracts.

## Out of scope

- Seeding, bounds, population size: the strategies and the sampler own those.
- Evaluating an objective on the host: there is no host-side objective contract in this
  package.
