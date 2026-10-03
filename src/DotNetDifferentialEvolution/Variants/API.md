# API.md — Variants

Namespace: `DotNetDifferentialEvolution.Variants`. A DE variant as one installable bundle,
and the four published ones. Installed through `DifferentialEvolutionBuilder.WithVariant`
and its `WithJde`/`WithJade`/`WithShade`/`WithLShade` wrappers
([package API](../API.md)). Everything not listed here is internal structure and may
change.

## Extension point ✅

```csharp
public interface IDeVariant
{
    DeVariantSetup Configure(in DeVariantConfiguration configuration);
    void Validate(in DeVariantConfiguration configuration,
        ITerminationStrategy terminationStrategy) { }          // default: accept
}

public readonly record struct DeVariantConfiguration(
    int PopulationSize, int GenomeSize,
    ReadOnlyMemory<double> LowerBound, ReadOnlyMemory<double> UpperBound);

public readonly record struct DeVariantSetup
{
    public required IMutationStrategy MutationStrategy { get; init; }
    public IControlParameterProvider? ControlParameterProvider { get; init; }
    public IGenerationStrategy? GenerationStrategy { get; init; }
    public ISelectionStrategy? SelectionStrategy { get; init; }  // null: greedy default
    public int ArchiveCapacity { get; init; }                    // 0: no archive
}
```

`Configure` is called once, when the variant is chosen on the builder; `PopulationSize`
is the initial size. `Validate` is called from `Build`, after the builder's own checks;
it rejects by throwing `InvalidOperationException`. A third-party variant goes through
exactly the same checks as a built-in one.

## Published variants ✅

```csharp
public sealed class JdeVariant : IDeVariant
{
    public JdeVariant(double initialMutationForce = 0.5, double initialCrossoverProbability = 0.9);
}
public sealed class JadeVariant : IDeVariant
{
    public JadeVariant(double pBestRate = 0.1, double archiveSizeRate = 1.0,
        double adaptationRate = 0.1);
}
public sealed class ShadeVariant : IDeVariant
{
    public ShadeVariant(double pBestRate = 0.2, double archiveSizeRate = 1.0, int memorySize = 100);
}
public sealed class LShadeVariant : IDeVariant
{
    public LShadeVariant(long maxEvaluationNumber, double pBestRate = 0.11,
        double archiveSizeRate = 2.6, int memorySize = 6);
    public void Validate(in DeVariantConfiguration configuration,
        ITerminationStrategy terminationStrategy);
}
```

| Variant | Mutation | F, CR and hook (one object) | Tie | Archive |
|---|---|---|---|---|
| jDE | `RandMutationStrategy` | `JdeStrategy` | survives | none |
| JADE | `CurrentToPBestMutationStrategy(p)` | `JadeStrategy` | parent kept | `round(rate·N)` |
| SHADE | `CurrentToPBestMutationStrategy(min(2/N, p), p)` | `ShadeStrategy` | survives | `round(rate·N)` |
| L-SHADE | `CurrentToPBestMutationStrategy(p)` | `LShadeStrategy` | survives | `round(rate·N)` |

Rounding is half away from zero.

## Errors

| Situation | Behaviour |
|---|---|
| `archiveSizeRate < 0` (JADE, SHADE, L-SHADE) | `ArgumentOutOfRangeException` |
| `maxEvaluationNumber <= 0` (L-SHADE) | `ArgumentOutOfRangeException` |
| L-SHADE with a `LimitEvaluationNumberTerminationStrategy` whose limit differs from its budget | `InvalidOperationException` from `Build` |

## Out of scope

- The adaptation rules themselves: [Algorithms](../Algorithms/Jde/API.md) (Jde, Jade,
  Shade, Lshade).
