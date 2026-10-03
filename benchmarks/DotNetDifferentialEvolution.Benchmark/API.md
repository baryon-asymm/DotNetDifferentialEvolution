# API.md — DotNetDifferentialEvolution.Benchmark

A library since 2026-10-03, referenced only by
[Benchmark.Runner](../DotNetDifferentialEvolution.Benchmark.Runner/API.md), whose
command line starts what is declared here.

## Convergence comparison ✅

```csharp
public static class ConvergenceComparison
{
    public static void Run();
}
```

Runs DE/rand/1/bin, jDE, JADE and SHADE with population 100 and L-SHADE with
`18·D = 540`, each on all processors and stopped at the same evaluation budget, and
prints the best value reached (lower is better). The objectives are the shared library's
`RastriginEvaluator` and `AckleyEvaluator` at 30 dimensions. Seeded (seed 1): the table
repeats on one machine; a machine with another processor count runs other worker
counts, hence other runs.

## Children

- [BenchmarkTesters](BenchmarkTesters/API.md) — the BenchmarkDotNet class.
- [Benchmark.Runner](../DotNetDifferentialEvolution.Benchmark.Runner/API.md) — the
  executable (a sibling directory, not a subdirectory).
