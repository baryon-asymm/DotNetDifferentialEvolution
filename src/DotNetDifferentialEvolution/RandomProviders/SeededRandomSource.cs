using System.Runtime.CompilerServices;

namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// The engine's <see cref="IRandomSource"/>: draws straight from a worker's
/// <see cref="SeededRandomProvider"/>.
/// </summary>
/// <remarks>
/// <see cref="SeededRandomProvider"/> is sealed, so every call below resolves to a direct,
/// inlinable target. Used as a <see langword="struct"/> type argument the interface calls
/// disappear too, leaving the generator's arithmetic inline in the helper's loop.
/// </remarks>
internal readonly struct SeededRandomSource : IRandomSource
{
    private readonly SeededRandomProvider _provider;

    public SeededRandomSource(
        SeededRandomProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Next(
        int maxValue) => _provider.Next(maxValue);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextULong() => _provider.NextULong();
}
