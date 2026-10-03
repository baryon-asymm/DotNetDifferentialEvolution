# API.md — DotNetDifferentialEvolution.Benchmark.Runner

A console executable; nobody references it. Its interface is the command line.

## Command line ✅

```text
dotnet run -c Release --project benchmarks/DotNetDifferentialEvolution.Benchmark.Runner
    → BenchmarkDotNet runs SimpleSumTester (throughput of one generation)

dotnet run -c Release --project benchmarks/DotNetDifferentialEvolution.Benchmark.Runner -- convergence
    → ConvergenceComparison.Run(): a table, best objective of each variant on
      Rastrigin and Ackley, 30-D, 300 000 evaluations each
```

The mode word is compared ignoring case; any other first argument, or none, runs
BenchmarkDotNet.
