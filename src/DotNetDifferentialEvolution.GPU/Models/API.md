# API.md — GPU Models

Namespace: `DotNetDifferentialEvolution.GPU.Models`. The data shapes every other node of
the GPU package passes around: a population on the device, its host-side owner, and the
result types. Everything not listed here is internal structure and may change.

## Device population ✅

```csharp
public readonly struct DevicePopulation
{
    public DevicePopulation(
        ArrayView1D<double, Stride1D.Dense> fitnessFunctionValues,
        ArrayView2D<double, Stride2D.DenseX> individuals);

    public ArrayView1D<double, Stride1D.Dense> FitnessFunctionValues { get; }
    public ArrayView2D<double, Stride2D.DenseX> Individuals { get; }
    public int PopulationSize { get; }   // Individuals.Extent.X
    public int VectorSize { get; }       // Individuals.Extent.Y
}
```

A view, not an owner: it is what a kernel receives. `Individuals[i, j]` is gene `j` of
individual `i`; the layout is `Stride2D.DenseX`, so the individual index is the dense
dimension and neighbouring threads (neighbouring `i`) read neighbouring addresses.
`FitnessFunctionValues[i]` is the fitness of individual `i`; lower is better everywhere
in the package.

## Host population ✅

```csharp
public class HostPopulation
{
    public HostPopulation(
        MemoryBuffer1D<double, Stride1D.Dense> fitnessFunctionValues,
        MemoryBuffer2D<double, Stride2D.DenseX> individuals);

    public MemoryBuffer1D<double, Stride1D.Dense> FitnessFunctionValues { get; }
    public MemoryBuffer2D<double, Stride2D.DenseX> Individuals { get; }
    public DevicePopulation DevicePopulation { get; }
}
```

Holds the two device buffers of one population. `DevicePopulation` builds a new view
over them on every read. It does not dispose the buffers: whoever allocated them frees
them.

## Results ✅

```csharp
public class OptimizationResult
{
    public OptimizationResult(double fitnessFunctionValue, IEnumerable<double> individual);
    public double FitnessFunctionValue { get; }
    public ReadOnlyCollection<double> Individual { get; }
}

public class Individual
{
    public Individual(double fitnessFunctionValue, IEnumerable<double> vector);
    public double FitnessFunctionValue { get; }
    public ReadOnlyCollection<double> Vector { get; }
}
```

Both copy the sequence they are given into a read-only collection; they are host-side
snapshots, independent of any device buffer.

## Errors

| Situation | Behaviour |
|---|---|
| A `null` sequence passed to `OptimizationResult` or `Individual` | `ArgumentNullException` from LINQ's `ToArray`, in the constructor |
| Buffers of mismatched population sizes passed to `HostPopulation` | Not detected; the views simply disagree |

## Side effects

None. The types neither allocate nor free device memory.

## Out of scope

- Allocating or freeing device buffers: the kernel controller does that
  ([Kernels](../Controllers/Kernels/API.md)).
- Any check that the 1D and 2D buffers describe the same population.
