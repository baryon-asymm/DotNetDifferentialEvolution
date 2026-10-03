using System.Reflection;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The GPU package as the guards of its ACCEPTANCE.md (v1 checks 5a, 7a, 8a–8d) read it: its node, found by
/// <see cref="ProtocolConfig.GpuPackagePath"/>; its assembly, which also holds every child node without a project of its
/// own; and its C# sources on disk. A stale path fails loudly instead of scoping nothing. This tree's own addition to
/// the kit (this node's BOOT.md, Deviations from the kit).
/// </summary>
internal static class GpuPackage
{
    /// <summary>The package's node.</summary>
    public static Node Node =>
        Tree.Nodes.SingleOrDefault(candidate => candidate.RelativePath == ProtocolConfig.GpuPackagePath)
        ?? throw new InvalidOperationException($"{ProtocolConfig.GpuPackagePath} is not a node of the tree: ProtocolConfig.GpuPackagePath is stale");

    /// <summary>The package's assembly, loaded from this test project's build output.</summary>
    public static Assembly Assembly =>
        NodeAssemblies.AssemblyOf(Node)
        ?? throw new InvalidOperationException($"{ProtocolConfig.GpuPackagePath} has no assembly in the build output of the protocol tests");

    /// <summary>Whether a node is the package's node or lies below it: a future child node stays in scope.</summary>
    public static bool Covers(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.RelativePath == ProtocolConfig.GpuPackagePath || node.RelativePath.StartsWith(ProtocolConfig.GpuPackagePath + "/", StringComparison.Ordinal);
    }

    /// <summary>Every <c>*.cs</c> file under the package's directory, child nodes included, build output and the other
    /// directories <see cref="Tree.IsSkipped"/> names excluded; ordinal.</summary>
    public static IReadOnlyList<string> SourceFiles()
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(Node.Directory);
        while (pending.TryPop(out var directory))
        {
            files.AddRange(Directory.GetFiles(directory, "*.cs"));
            foreach (var child in Directory.GetDirectories(directory).Where(child => !Tree.IsSkipped(child)))
            {
                pending.Push(child);
            }
        }

        return [.. files.Order(StringComparer.Ordinal)];
    }
}
