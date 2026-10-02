# API.md — DotNetDifferentialEvolution.Benchmark

A console executable; nobody references it. Its interface is the command line.

## Command line ✅

```text
dotnet run -c Release --project benchmarks/DotNetDifferentialEvolution.Benchmark
    → BenchmarkDotNet runs SimpleSumTester (throughput of one generation)

dotnet run -c Release --project benchmarks/DotNetDifferentialEvolution.Benchmark -- convergence
    → a table: best objective of each variant on Rastrigin and Ackley, 30-D,
      300 000 evaluations each
```

## Convergence comparison ✅

```csharp
public static class ConvergenceComparison
{
    public static void Run();
}
```

Runs DE/rand/1/bin, jDE, JADE and SHADE with population 100 and L-SHADE with
`18·D = 540`, each on all processors and stopped at the same evaluation budget, and
prints the best value reached (lower is better). Unseeded: each run prints new numbers.

## Children

- [BenchmarkTesters](BenchmarkTesters/API.md) — the BenchmarkDotNet class.
- [Functions](Functions/API.md) — the two objectives of the comparison.
- [RandomGenerators](RandomGenerators/API.md) — a seeded provider nothing uses.
