# BOOT.md — Benchmark/RandomGenerators

## Purpose

A seeded provider for benchmarks, so that a run measures the same work every time. Its
history before the directory rename (`b501c94`) was not traced. Today the engine
derives a `SeededRandomProvider` per worker from `ProblemContext.RandomSeed`
(`3f3d394`), and the throughput benchmark seeds through that instead.

## Invariants

- **Same seed, same stream** (it wraps `System.Random(seed)`).

## Dependencies

None.

Outside the tree: `DotNetOptimization.Abstractions` 1.0.0 (`BaseRandomProvider`).

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)).

## Acceptance criteria

- [ ] ⚠ Dead code: nothing constructs `DeterminedRandomProvider` (searched
      2026-10-02); `SimpleSumTester` still imports its namespace. It duplicates
      `DeterministicRandomProvider` in
      [Tests.Shared/Fakes](../../../tests/DotNetDifferentialEvolution.Tests.Shared/Fakes/API.md).
- [ ] ⚠ The constructor parameter is named `Seed`, against the repository's
      camel-case parameters.

## Taboos

- **No new use of it.** Seed through the context or the builder.
