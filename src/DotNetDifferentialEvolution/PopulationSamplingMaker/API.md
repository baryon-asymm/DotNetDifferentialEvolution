# API.md — PopulationSamplingMaker

Namespace: `DotNetDifferentialEvolution.PopulationSamplingMaker`. The built-in initial
population: uniform in the box. Everything not listed here is internal structure and may
change.

## Uniform sampler ✅

```csharp
public class UniformRandomSamplingMaker : IPopulationSamplingMaker
{
    public UniformRandomSamplingMaker(ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound);
    public UniformRandomSamplingMaker(ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound, BaseRandomProvider randomProvider);

    public void UseRandomProvider(BaseRandomProvider randomProvider);
    public void SamplePopulation(Span<double> population);
}
```

Implements [`IPopulationSamplingMaker`](../Interfaces/API.md). **Lower bound first.**
Gene `k` of the flat buffer is `lower[k mod D] + NextDouble() * (upper - lower)`, so in
`[lower, upper)`, with `D` the bounds' length; the whole buffer is filled. Without a
provider it draws from the family's default `RandomProvider`; `UseRandomProvider` (called
by the builder for a seeded run) replaces it.

## Errors

| Situation | Behaviour |
|---|---|
| `null` provider | `ArgumentNullException` |
| Empty bounds with a non-empty buffer | `DivideByZeroException` (`k mod 0`) |
| Bounds of different lengths, or `lower > upper` | Not checked |

## Side effects

Draws from its provider.

## Out of scope

- Other designs (Latin hypercube, opposition-based, user points).
