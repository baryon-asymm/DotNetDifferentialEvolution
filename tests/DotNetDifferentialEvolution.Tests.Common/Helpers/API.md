# API.md — Tests.Common/Helpers

Namespace: `DotNetDifferentialEvolution.Tests.Common.Helpers`. Building a
`ProblemContext` by hand, without the builder, for tests that drive the engine's parts
directly.

## Context factory ✅

```csharp
public static class ProblemContextHelper
{
    public static ProblemContext CreateContext(int populationSize,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy, int workersCount = 1, int? seed = null,
        IGenerationStrategy? generationStrategy = null);
    public static ProblemContext CreateContext(int populationSize,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        IControlParameterProvider? controlParameterProvider, int? seed = null);
}
```

Samples the population uniformly in the evaluator's bounds — from
`new SeededRandomProvider(seed)` when seeded, a new unseeded `RandomProvider` otherwise — evaluates it once with `Evaluate(genes)`, and
returns a context carrying the seed as `RandomSeed`, the generation strategy and the
control-parameter provider (both init-only on the context, hence the parameters). Without
a provider the context is one the builder would refuse for a strategy that reads F and
CR; `UnitTests/AlgorithmExecutors` builds exactly that.

## Buffers ✅

```csharp
public class PopulationHelper
{
    public PopulationHelper(int populationSize, int genomeSize);
    public Memory<double> Population { get; }
    public Memory<double> TrialPopulation { get; }
    public Memory<double> PopulationFfValues { get; }
    public Memory<double> TrialPopulationFfValues { get; }
    public void InitializePopulationWithRandomValues(ReadOnlySpan<double> lowerBounds,
        ReadOnlySpan<double> upperBounds, BaseRandomProvider? random = null);
    public void EvaluatePopulationFfValues(IFitnessFunctionEvaluator evaluator);
}
```

The four buffers are slices of one array.

## Errors

| Situation | Behaviour |
|---|---|
| `null` evaluator | `ArgumentNullException` |
| Evaluator bounds of different lengths | `ArgumentException` |
