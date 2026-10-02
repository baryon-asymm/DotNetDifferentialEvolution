# API.md — Benchmark/BenchmarkTesters

Namespace: `DotNetDifferentialEvolution.Benchmark.BenchmarkTesters`. BenchmarkDotNet
classes.

## Throughput benchmark ✅

```csharp
public class SimpleSumTester
{
    public SimpleSumTester();
    [Benchmark] public void SimpleFitnessFunctionEvaluatorBenchmark();
}
```

One call is one generation's work for worker 0 of a single-worker engine: mutate,
cross, evaluate and select all 300 individuals of a 20-gene population (box ±10), with
the legacy `MutationStrategy` (F 0.5, CR 0.9) and the cheap objective `Σ xᵢ`, seeded
`0x12345678`. The context is never swapped, so every call does the same work on the
same population.
