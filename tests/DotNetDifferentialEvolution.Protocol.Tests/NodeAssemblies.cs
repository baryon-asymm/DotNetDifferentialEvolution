using System.Reflection;

namespace ProtocolChecks;

/// <summary>
/// The assembly each node's project builds, loaded by name from this test project's build output, and a type's own
/// node by the namespace attribution AGENTS.md §1 defines: the deepest node whose namespace equals, or prefixes at a
/// dot boundary, the type's own namespace. A child node without a project of its own compiles into its nearest
/// ancestor's assembly under its own namespace; every check reads that one attribution.
/// </summary>
internal static class NodeAssemblies
{
    private static readonly Lazy<IReadOnlyDictionary<Node, Assembly>> AssembliesLazy = new(Load);

    private static readonly Lazy<IReadOnlyList<Node>> CodeNodesLazy = new(
        () => [.. Tree.Nodes.Where(node => AssemblyOf(node) is not null).OrderBy(node => node.RelativePath, StringComparer.Ordinal)]);

    /// <summary>The assemblies of the nodes that hold a project, loaded by the name the project gives them.</summary>
    public static IReadOnlyDictionary<Node, Assembly> Assemblies => AssembliesLazy.Value;

    /// <summary>Every node whose code lives in one of the tree's assemblies: a node with its own project, or a child node
    /// compiling into its nearest ancestor's (<see cref="AssemblyOf"/>). Ordered by path.</summary>
    public static IReadOnlyList<Node> CodeNodes => CodeNodesLazy.Value;

    /// <summary>The library assemblies of the tree: every node assembly that does not reference xunit, by name.</summary>
    public static IReadOnlyList<Assembly> LibraryAssemblies =>
        [.. Assemblies.Values.Distinct().Where(assembly => !IsTestAssembly(assembly)).OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)];

    /// <summary>The node whose project holds a node's compiled types: itself if it has a project, or the nearest ancestor
    /// that does. Null for a node with no project anywhere in its chain up to the root.</summary>
    public static Node? ProjectNodeOf(Node node) =>
        Tree.Nodes.Where(candidate => candidate == node || node.IsDescendantOf(candidate))
            .OrderByDescending(candidate => candidate.RelativePath.Length)
            .FirstOrDefault(Assemblies.ContainsKey);

    /// <summary>The assembly that holds a node's compiled types (<see cref="ProjectNodeOf"/>), or null.</summary>
    public static Assembly? AssemblyOf(Node node)
    {
        var projectNode = ProjectNodeOf(node);
        return projectNode is null ? null : Assemblies[projectNode];
    }

    /// <summary>The node whose project built the assembly, or null for an assembly from outside the tree. Names only the
    /// project node, never a project-less child whose code the same assembly also carries.</summary>
    public static Node? NodeOf(Assembly assembly) => Assemblies.FirstOrDefault(pair => pair.Value == assembly).Key;

    /// <summary>The node a type belongs to. A type in a node's own assembly under the namespace that node declares as an
    /// exception (<see cref="ProtocolConfig.NamespaceExceptions"/>) belongs to that node; otherwise the deepest node whose
    /// namespace matches (<see cref="NodeOfNamespace"/>); otherwise, for a type with no attributable namespace (an
    /// unnamed compiler helper), the project node of the assembly it sits in. Null only for a type from outside the tree.</summary>
    public static Node? NodeOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var assemblyNode = NodeOf(type.Assembly);
        return assemblyNode is not null && ExemptNamespaceOf(assemblyNode) is { } exempt && type.Namespace == exempt
            ? assemblyNode
            : NodeOfNamespace(type.Namespace) ?? assemblyNode;
    }

    /// <summary>The namespace a node declares as its exception in <see cref="ProtocolConfig.NamespaceExceptions"/>, or null.</summary>
    public static string? ExemptNamespaceOf(Node node) =>
        ProtocolConfig.NamespaceExceptions.TryGetValue(node.RelativePath, out var exempt) ? exempt : null;

    /// <summary>The deepest node whose namespace equals, or prefixes at a dot boundary, the given namespace; null when no
    /// node matches.</summary>
    public static Node? NodeOfNamespace(string? ns)
    {
        if (ns is null)
        {
            return null;
        }

        Node? best = null;
        foreach (var node in Tree.Nodes)
        {
            if ((ns == node.Namespace || ns.StartsWith(node.Namespace + ".", StringComparison.Ordinal))
                && (best is null || node.Namespace.Length > best.Namespace.Length))
            {
                best = node;
            }
        }

        return best;
    }

    /// <summary>The node's own types: every type of its effective assembly that <see cref="NodeOf(Type)"/> attributes to
    /// this node and to no deeper one. Empty for a node with no assembly.</summary>
    public static IEnumerable<Type> TypesOf(Node node)
    {
        var assembly = AssemblyOf(node);
        return assembly is null ? [] : assembly.GetTypes().Where(type => NodeOf(type) == node);
    }

    /// <summary>A test assembly references xunit; its public types are its tests, described by its BOOT.md, not by API.md.</summary>
    public static bool IsTestAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetReferencedAssemblies().Any(reference => reference.Name is { } name && name.StartsWith("xunit", StringComparison.Ordinal));
    }

    /// <summary>The simple names an assembly grants <c>InternalsVisibleTo</c> to, public-key suffix stripped.</summary>
    public static IReadOnlyList<string> InternalsVisibleTo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return [.. assembly.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
            .Select(attribute => ((string)attribute.ConstructorArguments[0].Value!).Split(',')[0].Trim())];
    }

    private static Dictionary<Node, Assembly> Load()
    {
        var assemblies = new Dictionary<Node, Assembly>();
        foreach (var node in Tree.Nodes.Where(node => node.AssemblyName is not null))
        {
            try
            {
                assemblies[node] = Assembly.Load(new AssemblyName(node.AssemblyName!));
            }
            catch (FileNotFoundException e)
            {
                throw new InvalidOperationException(
                    $"the assembly of {node.Name} ({node.AssemblyName}) is not in the build output of the protocol tests: " +
                    "add a project reference to it in the test project file (every node project is referenced, none of its types used)", e);
            }
        }

        return assemblies;
    }
}
