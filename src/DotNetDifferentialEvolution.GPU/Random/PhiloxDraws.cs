namespace DotNetDifferentialEvolution.GPU.Random;

/// <summary>
/// The draws of one individual in one generation: Philox4x32-10 blocks under the key
/// <c>(seed, 0)</c> at the counters <c>(block, individual, generation, 0)</c>, block 0, 1, 2…,
/// consumed word by word. Draw <c>k</c> is therefore a pure function of
/// <c>(seed, individual, generation, k)</c>, the same on every backend (BOOT.md, invariant 3).
/// Generation 0 is the initial sampling.
/// </summary>
/// <remarks>A mutable struct held in a local of the kernel thread and passed by reference.</remarks>
/// <param name="seed">The run's seed, the first key word.</param>
/// <param name="individual">The individual's index, the second counter word.</param>
/// <param name="generation">The generation, the third counter word; 0 for the initial sampling.</param>
internal struct PhiloxDraws(int seed, int individual, int generation) : IDrawSource
{
    private const int WordsPerBlock = 4;

    private readonly uint _key0 = (uint)seed;
    private readonly uint _individual = (uint)individual;
    private readonly uint _generation = (uint)generation;
    private uint _block;
    private PhiloxBlock _words;
    private int _position = WordsPerBlock;

    /// <inheritdoc />
    public uint NextUInt()
    {
        if (_position == WordsPerBlock)
        {
            _words = Philox4x32x10.Generate(new PhiloxBlock(_block, _individual, _generation, 0), _key0, 0);
            _block++;
            _position = 0;
        }

        var word = _words.Word(_position);
        _position++;
        return word;
    }

    /// <inheritdoc />
    public int NextIndex(int n) => DrawConversions.ToIndex(NextUInt(), n);

    /// <inheritdoc />
    public ulong NextULong()
    {
        var high = NextUInt();
        return DrawConversions.ToULong(high, NextUInt());
    }

    /// <inheritdoc />
    public double NextUnitDouble()
    {
        var high = NextUInt();
        return DrawConversions.ToUnitDouble(high, NextUInt());
    }
}
