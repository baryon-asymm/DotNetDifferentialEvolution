# API.md — RandomProviders

Namespace: `DotNetDifferentialEvolution.RandomProviders`. The engine's random-number
generator, the distributions the adaptive variants sample, and the internal plumbing
that lets the per-gene draw inline. Everything not listed here is internal structure and
may change.

## Seeded generator ✅

```csharp
public sealed class SeededRandomProvider : BaseRandomProvider
{
    public SeededRandomProvider(int seed);

    public override int Next(int maxValue);
    public override double NextDouble();
    public ulong NextULong();
    public double NextGaussian(double mean, double standardDeviation);
}
```

xoshiro256** with its 256-bit state expanded from the 32-bit seed by SplitMix64.

- `Next(maxValue)`: uniform in `[0, maxValue)` by Lemire's multiply-shift on the high 32
  bits; bias at most `maxValue / 2^32`. `Next(0)` returns 0.
- `NextDouble()`: 53 random bits mapped onto `[0, 1)`.
- `NextULong()`: the raw 64-bit output; the cheapest draw.
- `NextGaussian(mean, sd)`: Box–Muller, returning the cosine half and caching the sine
  half on the instance for the next call; so two consecutive calls consume two uniforms.

**Not thread-safe**, by design: one instance per worker. Same seed, same stream.

## Distributions ✅

```csharp
public static class RandomDistributionHelper
{
    public static double NextGaussian(BaseRandomProvider randomProvider, double mean,
        double standardDeviation);
    public static double NextCauchy(BaseRandomProvider randomProvider, double location,
        double scale);
}
```

`NextGaussian` forwards to `SeededRandomProvider.NextGaussian` when given one (the
cached path); any other provider gets the plain transform from two uniforms, the second
normal discarded. `NextCauchy` is `location + scale * tan(pi * (u - 0.5))` from one
uniform.

## Internal to the assembly ✅

Used by the trial-construction helpers in `MutationStrategies`; not visible to
consumers.

```csharp
internal interface IRandomSource
{
    int Next(int maxValue);
    ulong NextULong();
}

internal readonly struct SeededRandomSource : IRandomSource
{
    public SeededRandomSource(SeededRandomProvider provider);
}

internal readonly struct ProviderRandomSource : IRandomSource
{
    public ProviderRandomSource(BaseRandomProvider provider);
}

internal static class RandomThreshold
{
    public static ulong Scale(double value);
}
```

- `IRandomSource` is consumed as a **struct type argument**, so the draw is resolved
  statically and inlines into the helper's loop.
- `SeededRandomSource` draws straight from the engine's generator.
- `ProviderRandomSource` adapts any `BaseRandomProvider`; it has no raw draw, so
  `NextULong()` is `RandomThreshold.Scale(provider.NextDouble())`.
- `RandomThreshold.Scale(p)` maps `[0, 1]` monotonically onto `[0, ulong.MaxValue]`
  (values ≤ 0 to 0, values ≥ 1 to `ulong.MaxValue`), so a Bernoulli test `u <= p`
  becomes an integer comparison with both sides scaled the same way.

## Errors

| Situation | Behaviour |
|---|---|
| `SeededRandomProvider.Next` with a negative bound | `ArgumentOutOfRangeException` |
| `null` provider to `RandomDistributionHelper` or `ProviderRandomSource` | `ArgumentNullException` |
| Negative or `NaN` standard deviation or scale | Not checked; the arithmetic result is returned |

## Side effects

Every draw advances the provider's state; `NextGaussian` also sets or clears its cached
spare normal.

## Out of scope

- Thread safety and sharing between workers: the engine gives each worker its own.
- How the engine derives worker seeds from the user's seed: `AlgorithmExecutors` and
  the builder own that.
