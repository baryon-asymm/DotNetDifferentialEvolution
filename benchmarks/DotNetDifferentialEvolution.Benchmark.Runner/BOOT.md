# BOOT.md — DotNetDifferentialEvolution.Benchmark.Runner

## Purpose

The command-line entry point of the benchmarks: `Program.cs` reads the mode and starts
either BenchmarkDotNet on the throughput class or the convergence table. Everything it
runs lives in the [Benchmark](../DotNetDifferentialEvolution.Benchmark/API.md) library.

Split from that project on 2026-10-03, when the maximum diagnostics came in: CA1515
asks an executable to make its types internal, and BenchmarkDotNet can only measure a
public class. A library may keep its types public, so the measured classes stay there
and only the entry point is an executable.

## Invariants

- **No logic beyond the mode switch.** Anything measured or printed belongs to the
  library, where it is documented and built with the solution.

## Dependencies

- [DotNetDifferentialEvolution.Benchmark](../DotNetDifferentialEvolution.Benchmark/API.md)
  — `ConvergenceComparison`.
- [BenchmarkTesters](../DotNetDifferentialEvolution.Benchmark/BenchmarkTesters/API.md)
  — `SimpleSumTester`.

Outside the tree: BenchmarkDotNet 0.14.0 (`BenchmarkRunner`).

## Constraints

Inherited from the parent ([Benchmark BOOT.md](../DotNetDifferentialEvolution.Benchmark/BOOT.md)):
settings from `benchmarks/Directory.Build.props`, Release only.

## Acceptance criteria

- [x] Builds with the solution, 0 warnings under the maximum diagnostics: 2026-10-03.
- [ ] Neither mode was run after the split.

## Taboos

- **No public type here.** An executable's types are internal (CA1515); a type that
  needs to be public belongs in the library.
