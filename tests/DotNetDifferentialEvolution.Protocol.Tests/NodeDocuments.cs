using System.Text.RegularExpressions;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// What a node's own <c>BOOT.md</c> declares under <c>## Dependencies</c> (AGENTS.md §6): every link that resolves to a
/// node's <c>API.md</c>, and the links that resolve to none. The canonical form itself (links or the single word
/// <c>None</c>, not both) is the linter's to check; this reads only what the reflection check compares.
/// </summary>
internal static partial class NodeDocuments
{
    [GeneratedRegex(
        @"^## Dependencies\s*$(.*?)(?=^## |\z)", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled)]
    private static partial Regex DependenciesSectionRegex();

    [GeneratedRegex(@"\]\(\s*<?([^)\s>]+)>?[^)]*\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    /// <summary>The nodes a BOOT.md declares in its <c>## Dependencies</c> section, and the API.md links resolving to no node.
    /// Links inside code fences are not read (the linter does not resolve them either).</summary>
    public static (IReadOnlySet<Node> Nodes, IReadOnlyList<string> Unresolved) DeclaredDependencies(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var boot = WithoutFences(File.ReadAllText(node.Boot).ReplaceLineEndings("\n"));
        var section = DependenciesSectionRegex().Match(boot);
        var declared = new HashSet<Node>();
        var unresolved = new List<string>();
        if (!section.Success)
        {
            return (declared, unresolved);
        }

        var byDirectory = Tree.Nodes.ToDictionary(n => Path.GetFullPath(n.Directory), n => n, StringComparer.OrdinalIgnoreCase);
        foreach (Match link in LinkRegex().Matches(section.Groups[1].Value))
        {
            var target = link.Groups[1].Value;
            if (!target.EndsWith("API.md", StringComparison.Ordinal))
            {
                continue;
            }

            var directory = Path.GetFullPath(Path.Combine(node.Directory, Path.GetDirectoryName(target) ?? string.Empty));
            if (byDirectory.TryGetValue(directory, out var found))
            {
                _ = declared.Add(found);
            }
            else
            {
                unresolved.Add(target);
            }
        }

        return (declared, unresolved);
    }

    private static string WithoutFences(string text)
    {
        var lines = text.Split('\n');
        var inside = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inside = !inside;
                lines[index] = string.Empty;
            }
            else if (inside)
            {
                lines[index] = string.Empty;
            }
        }

        return string.Join('\n', lines);
    }
}
