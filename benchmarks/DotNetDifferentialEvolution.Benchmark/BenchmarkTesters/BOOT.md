# BOOT.md — Benchmark/BenchmarkTesters

## Purpose

The engine's own cost per generation, with the objective made nearly free so that
mutation, crossover, selection and bookkeeping dominate. The measurement behind
performance changes to the executor and the mutation arithmetic.

## Invariants

- **The measured work is constant across iterations**: seeded context, no swap, so
  every call reads the same population and writes the same trial buffers.
- **It measures `AlgorithmExecutor.Execute` directly**, not through threads or the
  builder: no barrier, no orchestrator.

## Dependencies

- [AlgorithmExecutors](../../../src/DotNetDifferentialEvolution/AlgorithmExecutors/API.md),
  [AlgorithmExecutors/Interfaces](../../../src/DotNetDifferentialEvolution/AlgorithmExecutors/Interfaces/API.md)
  — the executor under measurement.
- [Models](../../../src/DotNetDifferentialEvolution/Models/API.md) — `ProblemContext`.
- [MutationStrategies](../../../src/DotNetDifferentialEvolution/MutationStrategies/API.md),
  [MutationStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/MutationStrategies/Interfaces/API.md),
  [SelectionStrategies](../../../src/DotNetDifferentialEvolution/SelectionStrategies/API.md),
  [SelectionStrategies/Interfaces](../../../src/DotNetDifferentialEvolution/SelectionStrategies/Interfaces/API.md),
  [TerminationStrategies](../../../src/DotNetDifferentialEvolution/TerminationStrategies/API.md)
  — the parts it is built from.
- [FitnessFunctionEvaluators](../../../tests/DotNetDifferentialEvolution.Tests.Shared/FitnessFunctionEvaluators/API.md),
  [Helpers](../../../tests/DotNetDifferentialEvolution.Tests.Shared/Helpers/API.md) —
  `SimpleSumEvaluator`, `ProblemContextHelper`.

Outside the tree: BenchmarkDotNet 0.14.0.

## Constraints

Inherited from the parent ([BOOT.md](../BOOT.md)). In addition:

- Run in Release through BenchmarkDotNet (`dotnet run -c Release`); a Debug run or a
  stopwatch is not a measurement.

## Acceptance criteria

- [x] Builds with the project, 0 warnings: 2026-10-02.
- [ ] Not run in this reconstruction: no BenchmarkDotNet figure is recorded here, so
      no claim about the engine's speed rests on this node.
- [ ] ⚠ Only the classic legacy scheme is measured; the p-best strategies, the adaptive
      hooks and the multi-worker barrier have no throughput benchmark.
- [ ] ⚠ Three fields (`_mutationStrategy`, `_selectionStrategy`, `_context`) are kept
      but never read after construction; `RandomProviders` and the `RandomGenerators`
      namespace are imported and unused.

## Taboos

- **No comparison of figures from different machines or configurations** as if they
  were one series.
