using Xunit;

namespace ProtocolChecks;

/// <summary>
/// The configuration itself: the tree has nodes, the root namespace is the tree's, and every node path
/// <see cref="ProtocolConfig"/> names is a node of the tree, so a renamed or removed node cannot leave a dead entry
/// behind that silently exempts or scopes nothing.
/// </summary>
public sealed class ConfigTests
{
    /// <summary>The walk finds nodes, and every node path named in the configuration is one of them.</summary>
    [Fact]
    public void EveryNodePathTheConfigurationNamesIsANode()
    {
        Assert.True(Tree.Nodes.Count > 1, $"found nothing: the walk from {Tree.Root} found {Tree.Nodes.Count} node(s), so it proves nothing");
        var paths = Tree.Nodes.Select(node => node.RelativePath).ToHashSet(StringComparer.Ordinal);
        var named = ProtocolConfig.NumericalNodes.Select(path => ("NumericalNodes", path))
            .Concat(ProtocolConfig.NamespaceExceptions.Keys.Select(path => ("NamespaceExceptions", path)));
        var problems = named.Where(entry => !paths.Contains(entry.path))
            .Select(entry => $"ProtocolConfig.{entry.Item1} names {entry.path}, which is not a node of the tree")
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The root namespace is the one the tree's code uses: some type of a library assembly lives under it.</summary>
    [Fact]
    public void TheRootNamespaceIsTheTreesOwn()
    {
        var namespaces = NodeAssemblies.LibraryAssemblies.SelectMany(assembly => assembly.GetTypes()).Select(type => type.Namespace).OfType<string>().ToHashSet(StringComparer.Ordinal);
        Assert.True(namespaces.Count > 0, "found nothing: no library assembly holds a type with a namespace, so the walk proves nothing");
        if (ProtocolConfig.RootNamespace.Length == 0)
        {
            // An empty root: the root node owns the global namespace, so no library type may live there.
            var global = NodeAssemblies.LibraryAssemblies.SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.Namespace is null && !type.Name.StartsWith('<') && !IsTopLevelStatementsProgram(type))
                .Select(type => type.FullName).ToList();
            Assert.True(global.Count == 0, "with an empty ProtocolConfig.RootNamespace no library type may live in the global namespace: " + string.Join(", ", global));
            return;
        }

        var root = ProtocolConfig.RootNamespace;
        Assert.True(
            namespaces.Any(ns => ns == root || ns.StartsWith(root + ".", StringComparison.Ordinal)),
            $"ProtocolConfig.RootNamespace is {ProtocolConfig.RootNamespace}, and no library type lives under it; the namespaces found are: " +
            string.Join(", ", namespaces.Order(StringComparer.Ordinal).Take(10)));
    }

    /// <summary>The class the compiler synthesizes for top-level statements: it lives in the global namespace by language
    /// rule and holds the entry point <c>&lt;Main&gt;$</c>.</summary>
    private static bool IsTopLevelStatementsProgram(Type type) =>
        type.GetMethod("<Main>$", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public) is not null;
}
