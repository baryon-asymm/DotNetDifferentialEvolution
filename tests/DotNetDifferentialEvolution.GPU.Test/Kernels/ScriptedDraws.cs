using DotNetDifferentialEvolution.GPU.Random;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>The kind of one scripted draw.</summary>
internal enum DrawKind
{
    /// <summary>An index, <see cref="IDrawSource.NextIndex"/>.</summary>
    Index = 1,

    /// <summary>A 64-bit word, <see cref="IDrawSource.NextULong"/>.</summary>
    ULong = 2,
}

/// <summary>One scripted draw: its kind, its value, and for an index the range it was drawn from (0: any).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Value">The index or the 64-bit word.</param>
/// <param name="Range">For an index, the <c>n</c> the caller must ask for; 0 accepts any.</param>
internal readonly record struct ScriptedDraw(DrawKind Kind, ulong Value, int Range = 0)
{
    /// <summary>An index draw.</summary>
    /// <param name="index">The index returned.</param>
    /// <param name="range">The <c>n</c> the caller must ask for; 0 accepts any.</param>
    /// <returns>The draw.</returns>
    public static ScriptedDraw Index(int index, int range = 0) => new(DrawKind.Index, (ulong)index, range);

    /// <summary>A 64-bit draw.</summary>
    /// <param name="word">The word returned.</param>
    /// <returns>The draw.</returns>
    public static ScriptedDraw Word(ulong word) => new(DrawKind.ULong, word);
}

/// <summary>
/// A draw source that replays a script, for the DE step called on the host (ACCEPTANCE.md, checks
/// 1c, 1d, 1g). The script is consumed in order; a call of the wrong kind, an index outside
/// <c>[0, n)</c> or a range other than the scripted one throws, so the test sees the step consume
/// exactly the draws it was given, in the order given. Host only: it holds an array.
/// </summary>
/// <param name="script">The draws, in order.</param>
internal struct ScriptedDraws(IReadOnlyList<ScriptedDraw> script) : IDrawSource
{
    private readonly IReadOnlyList<ScriptedDraw> _script = script;

    /// <summary>Gets the number of draws consumed so far.</summary>
    public int Consumed { get; private set; }

    /// <summary>Gets the number of 64-bit draws consumed so far.</summary>
    public int WordsConsumed { get; private set; }

    /// <inheritdoc />
    public int NextIndex(int n)
    {
        var draw = Take(DrawKind.Index);
        var index = (int)draw.Value;
        return draw.Range != 0 && draw.Range != n
            ? throw new InvalidOperationException($"Draw {Consumed - 1}: the step asked for an index in [0, {n}), the script holds one in [0, {draw.Range}).")
            : index >= n
                ? throw new InvalidOperationException($"Draw {Consumed - 1}: index {index} is outside [0, {n}).")
                : index;
    }

    /// <inheritdoc />
    public ulong NextULong()
    {
        WordsConsumed++;
        return Take(DrawKind.ULong).Value;
    }

    /// <inheritdoc />
    public readonly uint NextUInt() => throw new NotSupportedException("The DE step draws no raw 32-bit word.");

    /// <inheritdoc />
    public readonly double NextUnitDouble() => throw new NotSupportedException("The DE step draws no unit double.");

    private ScriptedDraw Take(DrawKind kind)
    {
        if (Consumed >= _script.Count)
        {
            throw new InvalidOperationException($"Draw {Consumed}: the step asked for a {kind}, the script is exhausted.");
        }

        var draw = _script[Consumed];
        Consumed++;
        return draw.Kind != kind
            ? throw new InvalidOperationException($"Draw {Consumed - 1}: the step asked for a {kind}, the script holds a {draw.Kind}.")
            : draw;
    }
}
