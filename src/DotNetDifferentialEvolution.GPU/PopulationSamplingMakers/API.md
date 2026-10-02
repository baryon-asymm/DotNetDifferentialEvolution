# API.md — GPU PopulationSamplingMakers

Namespace: `DotNetDifferentialEvolution.GPU.PopulationSamplingMakers`. The package's
sampler: uniform random points inside a box. Everything not listed here is internal
structure and may change.

## Uniform sampler ✅

```csharp
public class PopulationSamplingMaker : IPopulationSamplingMaker
{
    public PopulationSamplingMaker(
        int populationSize,
        IEnumerable<double> upperBound,
        IEnumerable<double> lowerBound);

    public int GetPopulationSize();
    public double[,] TakeSamples();
}
```

Implements [the contract](Interfaces/API.md). Note the parameter order: **upper bound
first, then lower**. Gene `j` of every individual is drawn as
`lower[j] + Random.Shared.NextDouble() * (upper[j] - lower[j])`, so it lies in
`[lower[j], upper[j])`. The vector size is the bounds' length. `TakeSamples` fills rows
in parallel (`Parallel.For`) and returns a new array on every call.

## Errors

| Situation | Behaviour |
|---|---|
| `populationSize <= 0` | `ArgumentException`, in the constructor |
| Bounds of different lengths | `ArgumentException`, in the constructor |
| `null` bounds | `ArgumentNullException` from LINQ, in the constructor |
| `lower[j] > upper[j]` | Not detected; genes are drawn from `(upper, lower]` instead |

## Side effects

Draws from the process-wide `Random.Shared`.

## Out of scope

- Reproducible sampling: there is no seed parameter.
- Any distribution other than uniform in the box.
