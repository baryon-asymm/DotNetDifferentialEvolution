namespace DotNetDifferentialEvolution.GPU.Random;

/// <summary>
/// The Philox4x32 counter-based generator with 10 rounds (Salmon, Moraes, Dror and Shaw,
/// "Parallel random numbers: as easy as 1, 2, 3", SC 2011; the reference implementation is
/// Random123's <c>philox.h</c>). A block of four words is a pure function of a 128-bit counter
/// and a 64-bit key, in integer arithmetic only, so every backend computes the same words.
/// </summary>
internal static class Philox4x32x10
{
    /// <summary>The multiplier applied to the first counter word (<c>PHILOX_M4x32_0</c>).</summary>
    public const uint Multiplier0 = 0xD2511F53;

    /// <summary>The multiplier applied to the third counter word (<c>PHILOX_M4x32_1</c>).</summary>
    public const uint Multiplier1 = 0xCD9E8D57;

    /// <summary>The Weyl increment of the first key word between rounds (<c>PHILOX_W32_0</c>).</summary>
    public const uint Weyl0 = 0x9E3779B9;

    /// <summary>The Weyl increment of the second key word between rounds (<c>PHILOX_W32_1</c>).</summary>
    public const uint Weyl1 = 0xBB67AE85;

    /// <summary>The number of rounds.</summary>
    public const int Rounds = 10;

    /// <summary>The four random words of one counter under one key.</summary>
    /// <param name="counter">The 128-bit counter.</param>
    /// <param name="key0">The first key word.</param>
    /// <param name="key1">The second key word.</param>
    /// <returns>The block of four random words.</returns>
    public static PhiloxBlock Generate(PhiloxBlock counter, uint key0, uint key1)
    {
        var block = Round(counter, key0, key1);
        for (var round = 1; round < Rounds; round++)
        {
            key0 += Weyl0;
            key1 += Weyl1;
            block = Round(block, key0, key1);
        }

        return block;
    }

    private static PhiloxBlock Round(PhiloxBlock counter, uint key0, uint key1)
    {
        var product0 = (ulong)Multiplier0 * counter.X0;
        var product1 = (ulong)Multiplier1 * counter.X2;
        var high0 = (uint)(product0 >> 32);
        var low0 = (uint)product0;
        var high1 = (uint)(product1 >> 32);
        var low1 = (uint)product1;
        return new PhiloxBlock(high1 ^ counter.X1 ^ key0, low1, high0 ^ counter.X3 ^ key1, low0);
    }
}
