namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// An <see cref="IRandomSource"/> over any <see cref="BaseRandomProvider"/>, for callers that
/// hold the abstraction rather than the engine's own generator.
/// </summary>
/// <remarks>
/// <see cref="BaseRandomProvider"/> exposes no raw 64-bit draw, so <see cref="NextULong"/> is
/// synthesized from <see cref="BaseRandomProvider.NextDouble"/> through
/// <see cref="RandomThreshold.Scale"/> — the same scaling applied to the probability it is
/// compared against. A provider that returns a scripted sequence of uniforms therefore decides
/// exactly the crossover it would have decided under the original floating-point test.
/// </remarks>
internal readonly struct ProviderRandomSource : IRandomSource
{
    private readonly BaseRandomProvider _provider;

    public ProviderRandomSource(
        BaseRandomProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
    }

    /// <inheritdoc />
    public int Next(
        int maxValue) => _provider.Next(maxValue);

    /// <inheritdoc />
    public ulong NextULong() => RandomThreshold.Scale(_provider.NextDouble());
}
