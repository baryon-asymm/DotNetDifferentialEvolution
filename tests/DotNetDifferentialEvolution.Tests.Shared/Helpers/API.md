# API.md — Tests.Shared/Helpers

Namespace: `DotNetDifferentialEvolution.Tests.Shared.Helpers`. Building a
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
}
```

Samples the population uniformly in the evaluator's bounds — from `new Random(seed)`
when seeded, `Random.Shared` otherwise — evaluates it once with `Evaluate(genes)`, and
returns a context carrying the seed as `RandomSeed` and the generation strategy (which
is init-only on the context, hence the parameter).

## Buffers ✅

```csharp
public class PopulationHelper
{
    public PopulationHelper(int populationSize, int genomeSize);
    public Memory<double> Population { get; }
    public Memory<double> TrialPopulation { get; }
    public Memory<double> PopulationFfValues { get; }
    public Memory<double> TrialPopulationFfValues { get; }
    public void InitializePopulationWithRandomValues();               // [0, 1), Random.Shared
    public void InitializePopulationWithRandomValues(ReadOnlySpan<double> lowerBounds,
        ReadOnlySpan<double> upperBounds, Random? random = null);
    public void EvaluatePopulationFfValues(IFitnessFunctionEvaluator evaluator);
}

public static class GenerateBoundsHelper
{
    public static ReadOnlyMemory<double> GenerateBounds(int length, double initialValue);
}
```

The four buffers are slices of one array.

## Errors

| Situation | Behaviour |
|---|---|
| `null` evaluator | `ArgumentNullException` |
| Evaluator bounds of different lengths | `ArgumentException` |
