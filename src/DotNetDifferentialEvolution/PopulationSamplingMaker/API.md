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
Gene `k` uses dimension `k mod D`, where `D` is the bounds' length. It consumes one
unit draw `u`. For finite ordered bounds the result lies in `[lower, upper]`. It uses
`u * (upper - lower) + lower` for a finite width and `(1 - u) * lower + u * upper`
when the width overflows. A rounded overshoot returns the upper endpoint.
⚠ 2026-11-13: the former half-open interval excluded fixed coordinates and values
rounded to the upper endpoint (`SamplesFiniteIntervals`).

The whole buffer is filled. Without a provider it draws from the family's default `RandomProvider`; `UseRandomProvider` (called
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
