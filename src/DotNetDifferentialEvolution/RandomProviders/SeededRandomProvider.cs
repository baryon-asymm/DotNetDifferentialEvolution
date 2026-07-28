namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// A reproducible <see cref="BaseRandomProvider"/> backed by a seeded <see cref="Random"/>.
/// </summary>
/// <remarks>
/// Deliberately <strong>not thread-safe</strong>: reproducibility requires that a stream be
/// consumed in a fixed order, which sharing one instance between workers destroys — the
/// interleaving would decide who gets which number. The engine gives every worker its own
/// instance, derived from the seed and the worker index.
/// </remarks>
public sealed class SeededRandomProvider : BaseRandomProvider
{
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeededRandomProvider"/> class.
    /// </summary>
    /// <param name="seed">The seed for the underlying generator.</param>
    public SeededRandomProvider(
        int seed)
    {
        _random = new Random(seed);
    }

    /// <inheritdoc />
    public override int Next(
        int maxValue)
        => _random.Next(maxValue);

    /// <inheritdoc />
    public override double NextDouble()
        => _random.NextDouble();
}
