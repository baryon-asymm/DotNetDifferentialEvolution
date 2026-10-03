namespace DotNetDifferentialEvolution.Tests.Common.Fakes;

/// <summary>
/// A <see cref="BaseRandomProvider"/> backed by a seeded <see cref="Random"/>. Unlike the
/// production provider (which uses <see cref="Random.Shared"/>), this yields a fully
/// reproducible stream, so integration and end-to-end runs can be repeated bit-for-bit and
/// any failure can be reproduced from its seed.
/// </summary>
/// <remarks>
/// Initializes a new instance seeded with <paramref name="seed"/>.
/// </remarks>
/// <param name="seed">The seed controlling the (reproducible) random stream.</param>
public sealed class DeterministicRandomProvider(
    int seed = 0) : BaseRandomProvider
{
    private readonly Random _random = new Random(seed);

    /// <inheritdoc />
    public override int Next(
        int maxValue) => _random.Next(maxValue);

    /// <inheritdoc />
    public override double NextDouble() => _random.NextDouble();
}
