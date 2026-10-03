using System;
using System.IO;
using System.Linq;

namespace ProtocolChecks;

/// <summary>
/// A node of the tree: a directory holding both documents. <see cref="RelativePath"/> is the directory path from the
/// tree root with '/' separators, empty for the root; <see cref="AssemblyName"/> is the name of the assembly the
/// project in the directory builds, if the directory holds one (a child node may compile into its nearest
/// ancestor's project instead, and a data-only node has none at all).
/// </summary>
internal sealed record Node(string RelativePath, string Directory, string? AssemblyName)
{
    /// <summary>The name used in messages: the path, or "the root".</summary>
    public string Name => RelativePath.Length == 0 ? "the root" : RelativePath;

    /// <summary>The full path of the node's BOOT.md.</summary>
    public string Boot => Path.Combine(Directory, "BOOT.md");

    /// <summary>The full path of the node's API.md.</summary>
    public string Api => Path.Combine(Directory, "API.md");

    /// <summary>The C# namespace this node's own code lives in (AGENTS.md §1: the namespace repeats the directory path
    /// from the tree root, the transparent grouping segments dropped). The one attribution every reflection check reads
    /// (<see cref="NodeAssemblies.NodeOf(Type)"/>).</summary>
    public string Namespace => RelativePath.Length == 0
        ? ProtocolConfig.RootNamespace
        : string.Join('.', new[] { ProtocolConfig.RootNamespace }.Concat(RelativePath.Split('/').Where(segment => !ProtocolConfig.TransparentSegments.Contains(segment))).Where(part => part.Length > 0));

    /// <summary>Whether this node lies strictly below <paramref name="other"/>.</summary>
    public bool IsDescendantOf(Node other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.RelativePath.Length == 0 ? RelativePath.Length > 0 : RelativePath.StartsWith(other.RelativePath + "/", StringComparison.Ordinal);
    }

    /// <summary>Whether the node lies under one of <see cref="ProtocolConfig.SourcePrefixes"/>.</summary>
    public bool IsSource => ProtocolConfig.SourcePrefixes.Any(prefix => RelativePath.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Whether the node lies under one of <see cref="ProtocolConfig.TestPrefixes"/>.</summary>
    public bool IsTest => ProtocolConfig.TestPrefixes.Any(prefix => RelativePath.StartsWith(prefix, StringComparison.Ordinal));
}
