namespace DotNetDifferentialEvolution.GPU.Random;

/// <summary>
/// A stream of random values for one individual in one launch. Implemented by structs and
/// consumed through a generic parameter, so the kernel calls it without virtual dispatch and a
/// test can script it.
/// </summary>
internal interface IDrawSource
{
    /// <summary>The next uniform 32-bit word.</summary>
    /// <returns>The word.</returns>
    uint NextUInt();

    /// <summary>A uniform index in <c>[0, n)</c>: one word, <see cref="DrawConversions.ToIndex"/>.</summary>
    /// <param name="n">The number of indices; at least 1.</param>
    /// <returns>The index.</returns>
    int NextIndex(int n);

    /// <summary>A uniform 64-bit word: two words, <see cref="DrawConversions.ToULong"/>.</summary>
    /// <returns>The word.</returns>
    ulong NextULong();

    /// <summary>A uniform double in <c>[0, 1)</c>: two words, <see cref="DrawConversions.ToUnitDouble"/>.</summary>
    /// <returns>The double.</returns>
    double NextUnitDouble();
}
