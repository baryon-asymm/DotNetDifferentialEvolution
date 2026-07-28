using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// A reproducible <see cref="BaseRandomProvider"/> backed by xoshiro256** — the generator the
/// engine gives to every worker.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately <strong>not thread-safe</strong>: reproducibility requires that a stream be
/// consumed in a fixed order, which sharing one instance between workers destroys — the
/// interleaving would decide who gets which number. The engine gives every worker its own
/// instance, derived from the seed and the worker index.
/// </para>
/// <para>
/// The class is <see langword="sealed"/> and holds its state inline on purpose. A call site
/// that holds a reference of this exact type gets direct, inlinable calls instead of virtual
/// dispatch through <see cref="BaseRandomProvider"/>, which is what keeps the per-gene crossover
/// draw from dominating trial construction.
/// </para>
/// </remarks>
public sealed class SeededRandomProvider : BaseRandomProvider
{
    private ulong _state0;
    private ulong _state1;
    private ulong _state2;
    private ulong _state3;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeededRandomProvider"/> class.
    /// </summary>
    /// <param name="seed">The seed for the underlying generator.</param>
    /// <remarks>
    /// The 32-bit seed is expanded into xoshiro's 256-bit state with SplitMix64, so seeds that
    /// differ by one — which is how the engine derives each worker's seed — still produce
    /// well-separated streams.
    /// </remarks>
    public SeededRandomProvider(
        int seed)
    {
        var expander = (ulong)(uint)seed;
        _state0 = SplitMix64(ref expander);
        _state1 = SplitMix64(ref expander);
        _state2 = SplitMix64(ref expander);
        _state3 = SplitMix64(ref expander);
    }

    /// <inheritdoc />
    public override int Next(
        int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);

        // Lemire's multiply-shift: uniform over [0, maxValue) to within a bias of
        // maxValue / 2^32, i.e. below 1e-7 for any population size this library runs.
        return (int)(((NextULong() >> 32) * (ulong)maxValue) >> 32);
    }

    /// <inheritdoc />
    public override double NextDouble()
        // 53 significant bits — the most a double can carry — mapped onto [0, 1).
        => (NextULong() >> 11) * (1.0 / (1UL << 53));

    /// <summary>
    /// Returns the generator's raw 64-bit output.
    /// </summary>
    /// <returns>A uniformly distributed 64-bit value.</returns>
    /// <remarks>
    /// The cheapest draw the provider offers — no conversion to floating point — and what the
    /// per-gene crossover test consumes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextULong()
    {
        var result = BitOperations.RotateLeft(_state1 * 5, 7) * 9;
        var shifted = _state1 << 17;

        _state2 ^= _state0;
        _state3 ^= _state1;
        _state1 ^= _state2;
        _state0 ^= _state3;
        _state2 ^= shifted;
        _state3 = BitOperations.RotateLeft(_state3, 45);

        return result;
    }

    private static ulong SplitMix64(
        ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;

        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;

        return z ^ (z >> 31);
    }
}
