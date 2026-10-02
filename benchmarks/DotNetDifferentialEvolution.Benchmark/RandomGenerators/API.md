# API.md — Benchmark/RandomGenerators

Namespace: `DotNetDifferentialEvolution.Benchmark.RandomGenerators`.

## Provider ✅

```csharp
public class DeterminedRandomProvider : BaseRandomProvider
{
    public DeterminedRandomProvider(int Seed);   // System.Random(Seed)
    public override int Next(int maxValue);
    public override double NextDouble();
}
```

A `BaseRandomProvider` over a seeded `System.Random`.
