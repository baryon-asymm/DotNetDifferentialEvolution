# API.md — MutationStrategies/Helpers

Namespace: `DotNetDifferentialEvolution.MutationStrategies.Helpers`. The shared
arithmetic of trial construction: SIMD vector operations, distinct-index draws, binomial
crossover with repair. All `internal`: used by the strategies of the parent node and by
the unit tests. Everything not listed here is internal structure and may change.

## Vector arithmetic ✅

```csharp
internal static class MutationMath
{
    public static void AssignBasePlusScaledDifference(Span<double> destination,
        ReadOnlySpan<double> baseVector, ReadOnlySpan<double> minuend,
        ReadOnlySpan<double> subtrahend, double mutationForce);
    public static void AddScaledDifference(Span<double> destination,
        ReadOnlySpan<double> minuend, ReadOnlySpan<double> subtrahend, double mutationForce);
    public static void AssignCurrentToTarget(Span<double> destination,
        ReadOnlySpan<double> current, ReadOnlySpan<double> target, double mutationForce);
}
```

`dest = base + F (a - b)`, `dest += F (a - b)`, `dest = cur + F (target - cur)`;
`Vector<double>` over whole vectors with a scalar tail. All spans have the destination's
length.

## Distinct indices ✅

```csharp
internal static class RandomIndexSelector
{
    public static void FillDistinctIndices(Span<int> indices, in MutationContext context);
    public static void FillDistinctIndices<TRandom>(Span<int> indices, int populationSize,
        int excludeIndex, TRandom randomSource) where TRandom : struct, IRandomSource;
}
```

Fills `indices` with mutually distinct values in `[0, populationSize)`, none equal to
`excludeIndex`: each draw is `Next(populationSize - 1)`, shifted past the excluded index;
a repeat is redrawn. Terminates only if `populationSize > indices.Length`.

## Crossover and repair ✅

```csharp
internal static class CrossoverHelper
{
    public static void BinomialCrossoverAndRepair(in MutationContext context,
        double crossoverProbability);
    public static void BinomialCrossoverAndRepair<TRandom>(int individualIndex,
        double crossoverProbability, ReadOnlySpan<double> population,
        Span<double> trialIndividual, ReadOnlySpan<double> lowerBound,
        ReadOnlySpan<double> upperBound, TRandom randomSource)
        where TRandom : struct, IRandomSource;
}
```

`trialIndividual` holds the mutant on entry. One gene index `jrand` is drawn first and
always taken from the mutant; every other gene is taken from the mutant when the raw draw
is `<= Scale(CR)`, else from the parent. A mutant gene below the lower bound becomes
`(lower + parent) / 2`, above the upper `(upper + parent) / 2`.

The context overloads draw through the worker's `SeededRandomProvider` when the engine
supplied one, otherwise through any `BaseRandomProvider`
([RandomProviders](../../RandomProviders/API.md)).

## Errors

None raised; a population size not above the number of indices requested loops forever.

## Side effects

Write their destination spans; advance the random source.

## Out of scope

- Choosing which vectors to combine: each strategy decides.
