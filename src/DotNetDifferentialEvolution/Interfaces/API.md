# API.md — Interfaces

Namespace: `DotNetDifferentialEvolution.Interfaces`. Two hooks a consumer may plug into a
run: the source of the initial population and the observer of every generation.
Everything not listed here is internal structure and may change.

## Initial population ✅

```csharp
public interface IPopulationSamplingMaker
{
    void SamplePopulation(Span<double> population);
    void UseRandomProvider(BaseRandomProvider randomProvider) { }   // default: ignore
}
```

`SamplePopulation` fills a flat, row-major gene buffer, `genomeSize` values per
individual, for the whole capacity. `UseRandomProvider` is called at most once, by the
builder, and only for a seeded run; an implementation that adopts the provider makes the
initial population reproducible, one that keeps the default stays unaffected.

## Observer ✅

```csharp
public interface IPopulationUpdatedHandler
{
    void Handle(Population population);
}
```

Called with the [`Population`](../Models/API.md) after each generation; the population is
the engine's own object, live, not a copy.

## Errors

Implementation-defined.

## Side effects

None of their own.

## Out of scope

- Bounds: a sampler receives none through this contract; the built-in one takes them in
  its constructor ([PopulationSamplingMaker](../PopulationSamplingMaker/API.md)).
