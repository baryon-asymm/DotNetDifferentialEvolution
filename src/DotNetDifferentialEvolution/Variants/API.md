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

Selecting a variant calls `Configure` with the initial population size and bounds. The first
build with those dimensions takes that setup. Each further build requests a new setup, as
does a build whose dimensions have changed. Return fresh mutable strategy objects on every
call. Within one setup, the provider and generation hook may point to the same object.
`Validate` runs after the builder's own checks and may throw `InvalidOperationException`.

⚠ 2026-11-12: this contract previously specified one `Configure` call per builder.
Two optimizers built from it then used the same adaptive strategies.

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
