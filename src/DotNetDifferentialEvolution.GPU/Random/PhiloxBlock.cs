namespace DotNetDifferentialEvolution.GPU.Random;

/// <summary>
/// Four 32-bit words: a Philox counter on the way in, four random words on the way out.
/// Four named fields rather than an array, because kernel code allocates no arrays.
/// </summary>
/// <param name="X0">The first word.</param>
/// <param name="X1">The second word.</param>
/// <param name="X2">The third word.</param>
/// <param name="X3">The fourth word.</param>
internal readonly record struct PhiloxBlock(uint X0, uint X1, uint X2, uint X3)
{
    /// <summary>The word at <paramref name="position"/>, 0 to 3; any other position gives the fourth word.</summary>
    /// <param name="position">The position of the word, 0 to 3.</param>
    /// <returns>The word.</returns>
    public uint Word(int position) => position switch
    {
        0 => X0,
        1 => X1,
        2 => X2,
        _ => X3,
    };
}
