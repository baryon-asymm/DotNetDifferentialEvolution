using Xunit;

namespace ProtocolChecks;

/// <summary>
/// The approve-and-compare discipline of a snapshot fact, written once for both snapshots. With no approved file the
/// actual text is written as the approved one and the fact fails, asking a human to read and commit it: a snapshot
/// nobody read approves nothing. On a mismatch the actual text is written beside the approved file as
/// <c>&lt;name&gt;.actual.txt</c> and the fact names the first differing line; on a match that file is deleted.
/// </summary>
internal static class ApprovedSnapshot
{
    /// <summary>Compares <paramref name="actual"/> with <c>&lt;SnapshotDirectory&gt;/&lt;name&gt;.approved.txt</c>.</summary>
    /// <param name="name">The snapshot's file stem, <c>PublicSurface</c> or <c>TreeContract</c>.</param>
    /// <param name="what">What the snapshot holds, for the messages.</param>
    /// <param name="actual">The text generated from the code now.</param>
    public static void Verify(string name, string what, string actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var directory = Path.Combine(Tree.Root, ProtocolConfig.SnapshotDirectory);
        var approvedPath = Path.Combine(directory, name + ".approved.txt");
        var actualPath = Path.Combine(directory, name + ".actual.txt");
        if (!File.Exists(approvedPath))
        {
            File.WriteAllText(approvedPath, actual);
            Assert.Fail($"No approved snapshot of {what} existed, so one was written to {approvedPath}. " +
                        "Read it, satisfy yourself that it is what the API.md of every node describes, commit it, and re-run.");
        }

        var approved = SurfaceText.Normalise(File.ReadAllText(approvedPath));
        var current = SurfaceText.Normalise(actual);
        if (approved == current)
        {
            File.Delete(actualPath);
            return;
        }

        File.WriteAllText(actualPath, actual);
        var approvedLines = approved.Split('\n');
        var currentLines = current.Split('\n');
        var first = Enumerable.Range(0, Math.Max(approvedLines.Length, currentLines.Length))
            .First(i => i >= approvedLines.Length || i >= currentLines.Length || approvedLines[i] != currentLines[i]);
        Assert.Fail(
            $"{char.ToUpperInvariant(what[0])}{what[1..]} no longer matches {Path.GetFileName(approvedPath)}.\n" +
            $"First difference at line {first + 1}: approved '{(first < approvedLines.Length ? approvedLines[first] : "<end>")}', " +
            $"actual '{(first < currentLines.Length ? currentLines[first] : "<end>")}'.\n" +
            $"What it is now was written to {actualPath}. If the change is intended, update the API.md of the node that owns the type " +
            "first, then replace the approved file with the actual one in the same commit.");
    }
}
