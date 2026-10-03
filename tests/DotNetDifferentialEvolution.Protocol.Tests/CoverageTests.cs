using Xunit;

namespace ProtocolChecks;

/// <summary>
/// Coverage (AGENTS.md §7, §13): every type a library assembly exports is named in the ✅ text of its own node's
/// API.md, in prose or in code (<see cref="ApiDeclarations.NamesType"/>, the one meaning of "named"). The snapshot
/// cannot catch an added and undescribed type: it is generated from the same code.
/// </summary>
public sealed class CoverageTests
{
    /// <summary>Every exported type of a library assembly is named in its node's API.md under ✅.</summary>
    [Fact]
    public void EveryExportedTypeOfALibraryAssemblyIsNamedInItsNodesApi()
    {
        var nodes = NodeAssemblies.CodeNodes.Where(node => !NodeAssemblies.IsTestAssembly(NodeAssemblies.AssemblyOf(node)!)).ToList();
        var walked = nodes.Sum(node => ExportedTypesOf(node).Count());
        Assert.True(walked > 0, "found nothing: no library node exports a type, so the walk proves nothing");
        var problems = nodes.SelectMany(UndocumentedTypeProblems).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<Type> ExportedTypesOf(Node node)
    {
        var exported = NodeAssemblies.AssemblyOf(node)!.GetExportedTypes().ToHashSet();
        return NodeAssemblies.TypesOf(node).Where(exported.Contains).OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    private static IEnumerable<string> UndocumentedTypeProblems(Node node)
    {
        var api = File.ReadAllText(node.Api);
        foreach (var type in ExportedTypesOf(node))
        {
            var name = TypeShape.SimpleName(type);
            if (!ApiDeclarations.NamesType(api, name))
            {
                yield return $"{Tree.Relative(node.Api)} never names {name} under ✅, which {node.Namespace} exports (AGENTS.md §7: no public type outside its node's API.md)";
            }
        }
    }
}
