namespace DotNetDifferentialEvolution.GPU.Devices.LibDevice;

/// <summary>
/// The three readings of a PTX text the post-link needs, written out by hand with the semantics of APThermo's three
/// regular expressions (commit <c>5fdd82c</c>, <c>LibDevicePostLink.cs</c>), which this package cannot use: the
/// analyzers ask for <c>[GeneratedRegex]</c>, and the generated types live outside the tree's namespaces, where the
/// protocol's reflection facts refuse them (BOOT.md).
/// <list type="bullet">
/// <item><see cref="CallSites"/>: <c>\bcall(?:\.uni)?\b[^;]*?(__ilgpu__nv_[A-Za-z0-9_]+)\s*,</c></item>
/// <item><see cref="Definitions"/>: <c>^\s*\.(visible|weak)?\s*\.func\b[^;]*?(__ilgpu__nv_[A-Za-z0-9_]+)\s*\(</c>, multiline</item>
/// <item><see cref="TargetSm"/>: <c>^\.target\s+sm_(\d+)</c>, multiline</item>
/// </list>
/// </summary>
internal static class PtxText
{
    /// <summary>The prefix of every ILGPU libdevice wrapper name.</summary>
    public const string WrapperNamePrefix = "__ilgpu__nv_";

    /// <summary>
    /// The wrapper names at <c>call</c> instructions, in order, repeats kept: after the word <c>call</c>, the first
    /// wrapper name followed by optional white space and a comma, before the statement's <c>;</c>. A parameter name or a
    /// <c>.func</c> header is never read: no <c>call</c> precedes it in its statement.
    /// </summary>
    /// <param name="ptx">The PTX text.</param>
    /// <returns>The full names, prefix included.</returns>
    public static IEnumerable<string> CallSites(string ptx)
    {
        ArgumentNullException.ThrowIfNull(ptx);
        var position = 0;
        while (FindWord(ptx, "call", position) is var call && call >= 0)
        {
            var (name, end) = FirstWrapperFollowedBy(ptx, call + "call".Length, StatementEnd(ptx, call), ',');
            if (name is null)
            {
                position = call + 1;
                continue;
            }

            yield return name;
            position = end;
        }
    }

    /// <summary>
    /// The wrapper names of <c>.visible .func</c> or <c>.weak .func</c> headers, in order, repeats kept: after the
    /// header, the first wrapper name followed by optional white space and an opening parenthesis, before a <c>;</c>.
    /// </summary>
    /// <param name="ptx">The PTX text.</param>
    /// <returns>The full names, prefix included.</returns>
    public static IEnumerable<string> Definitions(string ptx)
    {
        ArgumentNullException.ThrowIfNull(ptx);
        var position = 0;
        foreach (var lineStart in LineStarts(ptx))
        {
            if (lineStart < position || FuncHeaderEnd(ptx, lineStart) is not { } headerEnd)
            {
                continue;
            }

            var (name, end) = FirstWrapperFollowedBy(ptx, headerEnd, StatementEnd(ptx, headerEnd), '(');
            if (name is not null)
            {
                yield return name;
                position = end;
            }
        }
    }

    /// <summary>The number of the first line that starts <c>.target</c>, white space, <c>sm_</c> and digits.</summary>
    /// <param name="ptx">The PTX text.</param>
    /// <returns>The digits, or <see langword="null"/> when no such line exists.</returns>
    public static string? TargetSm(string ptx)
    {
        ArgumentNullException.ThrowIfNull(ptx);
        foreach (var lineStart in LineStarts(ptx))
        {
            if (!ptx.AsSpan(lineStart).StartsWith(".target", StringComparison.Ordinal))
            {
                continue;
            }

            var p = SkipWhiteSpace(ptx, lineStart + ".target".Length);
            if (p == lineStart + ".target".Length || !ptx.AsSpan(p).StartsWith("sm_", StringComparison.Ordinal))
            {
                continue;
            }

            var digits = p + "sm_".Length;
            var end = digits;
            while (end < ptx.Length && char.IsDigit(ptx[end]))
            {
                end++;
            }

            if (end > digits)
            {
                return ptx[digits..end];
            }
        }

        return null;
    }

    /// <summary>
    /// Where a header line's <c>.func</c> keyword ends, or <see langword="null"/>: from a line start, white space, a
    /// <c>.</c>, optionally <c>visible</c> or <c>weak</c>, white space, then <c>.func</c> not followed by a word character.
    /// </summary>
    private static int? FuncHeaderEnd(string ptx, int lineStart)
    {
        var p = SkipWhiteSpace(ptx, lineStart);
        if (p >= ptx.Length || ptx[p] != '.')
        {
            return null;
        }

        p++;
        if (ptx.AsSpan(p).StartsWith("visible", StringComparison.Ordinal))
        {
            p += "visible".Length;
        }
        else if (ptx.AsSpan(p).StartsWith("weak", StringComparison.Ordinal))
        {
            p += "weak".Length;
        }

        p = SkipWhiteSpace(ptx, p);
        var end = p + ".func".Length;
        return ptx.AsSpan(p).StartsWith(".func", StringComparison.Ordinal) && (end == ptx.Length || !IsWordCharacter(ptx[end]))
            ? end
            : null;
    }

    /// <summary>
    /// The first wrapper name in <c>[from, to)</c> followed by optional white space and <paramref name="terminator"/>,
    /// and the position after the terminator; a null name when there is none.
    /// </summary>
    private static (string? Name, int End) FirstWrapperFollowedBy(string ptx, int from, int to, char terminator)
    {
        var start = from;
        while (start < to && ptx.IndexOf(WrapperNamePrefix, start, to - start, StringComparison.Ordinal) is var found && found >= 0)
        {
            var nameEnd = found + WrapperNamePrefix.Length;
            while (nameEnd < ptx.Length && IsIdentifierCharacter(ptx[nameEnd]))
            {
                nameEnd++;
            }

            var next = SkipWhiteSpace(ptx, nameEnd);
            if (nameEnd > found + WrapperNamePrefix.Length && next < ptx.Length && ptx[next] == terminator)
            {
                return (ptx[found..nameEnd], next + 1);
            }

            start = found + 1;
        }

        return (null, 0);
    }

    /// <summary>The first index of <paramref name="word"/> at or after <paramref name="from"/> with a word boundary on both sides, or −1.</summary>
    private static int FindWord(string ptx, string word, int from)
    {
        var index = from;
        while (index < ptx.Length && ptx.IndexOf(word, index, StringComparison.Ordinal) is var found && found >= 0)
        {
            var end = found + word.Length;
            if ((found == 0 || !IsWordCharacter(ptx[found - 1])) && (end == ptx.Length || !IsWordCharacter(ptx[end])))
            {
                return found;
            }

            index = found + 1;
        }

        return -1;
    }

    private static int StatementEnd(string ptx, int from)
    {
        var semicolon = ptx.IndexOf(';', from);
        return semicolon < 0 ? ptx.Length : semicolon;
    }

    private static IEnumerable<int> LineStarts(string ptx)
    {
        yield return 0;
        for (var i = ptx.IndexOf('\n', StringComparison.Ordinal); i >= 0; i = ptx.IndexOf('\n', i + 1))
        {
            yield return i + 1;
        }
    }

    private static int SkipWhiteSpace(string ptx, int from)
    {
        var p = from;
        while (p < ptx.Length && char.IsWhiteSpace(ptx[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>The regular expressions' <c>[A-Za-z0-9_]</c>.</summary>
    private static bool IsIdentifierCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    /// <summary>The <c>\w</c> of a regular expression's <c>\b</c>.</summary>
    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c == '_';
}
