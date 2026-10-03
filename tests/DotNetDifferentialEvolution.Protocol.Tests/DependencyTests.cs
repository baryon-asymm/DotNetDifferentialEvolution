using Xunit;

namespace ProtocolChecks;

/// <summary>
/// Dependencies (AGENTS.md §6, §13): the <c>## Dependencies</c> of every node with code equals the nodes whose types its
/// own types use, in their shapes and in the bodies of their methods (a static call names its type in no signature; an
/// enum used only through a literal appears only in the signature of the method or field it reaches). A parent using
/// the types of its children declares nothing, and a link to a descendant is refused; an ancestor whose own types a node
/// uses is declared like a neighbour. A declared node with no types of its own (a data-only node, a node born ahead of
/// its code) is never reported unused: reflection cannot see a dependency on files read by path.
/// </summary>
public sealed class DependencyTests
{
    /// <summary>Every node declares the neighbours it uses in signatures and method bodies, and no other.</summary>
    [Fact]
    public void EveryNodeDeclaresTheNeighboursItUsesAndNoOther()
    {
        var nodes = NodeAssemblies.CodeNodes;
        var types = nodes.SelectMany(NodeAssemblies.TypesOf).ToList();
        Assert.True(types.Count > 0, "found nothing: no node of the tree has a type of its own, so the walk proves nothing");
        Assert.True(
            types.SelectMany(TypeShape.MethodsOf).Any(method => IlBody.Instructions(method).Any(instruction => instruction.Operand is not null)),
            "found nothing: the IL walk resolved no operand in any method body of the tree, so it proves nothing about dependencies read from bodies");
        var problems = nodes.SelectMany(ProblemsOf).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> ProblemsOf(Node node)
    {
        var (crossings, usedNodes) = Crossings(node);
        var (declared, unresolved) = NodeDocuments.DeclaredDependencies(node);
        var boot = Tree.Relative(node.Boot);
        foreach (var link in unresolved)
        {
            yield return $"{boot} links {link} under ## Dependencies, and no node has that API.md";
        }

        var declaredPaths = declared.Select(d => d.RelativePath).ToHashSet(StringComparer.Ordinal);
        foreach (var used in crossings.Keys.Where(used => !declaredPaths.Contains(used)))
        {
            yield return $"{boot} does not declare {usedNodes[used].Name}, but {node.Name} uses its types: {string.Join(", ", crossings[used].Take(6))}" +
                         (crossings[used].Count > 6 ? $" and {crossings[used].Count - 6} more" : string.Empty);
        }

        foreach (var unused in declared.Where(d => !crossings.ContainsKey(d.RelativePath)).OrderBy(d => d.RelativePath, StringComparer.Ordinal))
        {
            if (unused == node)
            {
                yield return $"{boot} declares the node itself under ## Dependencies (AGENTS.md §6)";
            }
            else if (unused.IsDescendantOf(node))
            {
                yield return $"{boot} declares its descendant {unused.Name}; a parent owns its children and declares no dependency on them (AGENTS.md §6)";
            }
            else if (NodeAssemblies.TypesOf(unused).Any())
            {
                yield return $"{boot} declares {unused.Name}, but no type of {node.Name} refers to it: the dependency went away and the document did not, or it was never real";
            }
        }
    }

    /// <summary>Every neighbour or ancestor node a node's own types refer to, with every (type → referenced type) pair
    /// that shows it; <see cref="ProblemsOf"/> names the first six.</summary>
    private static (SortedDictionary<string, SortedSet<string>> Crossings, Dictionary<string, Node> UsedNodes) Crossings(Node node)
    {
        var crossings = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var usedNodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var type in NodeAssemblies.TypesOf(node))
        {
            foreach (var referenced in TypeShape.ReferencedTypes(type))
            {
                // Adaptation for this tree (BOOT.md, ## Deviations from the kit): the compiler's namespaceless helpers
                // (<PrivateImplementationDetails> and its __StaticArrayInitTypeSize=N, <>z__ReadOnlyArray<T> for a
                // collection expression) fall to the assembly's project node, which for a subnode of a test project is
                // its ancestor; nobody wrote that crossing, so it is no dependency.
                var target = NodeAssemblies.NodeOf(referenced);
                if (target is null || target == node || target.IsDescendantOf(node) || IsCompilerHelper(referenced))
                {
                    continue;
                }

                usedNodes[target.RelativePath] = target;
                if (!crossings.TryGetValue(target.RelativePath, out var users))
                {
                    crossings[target.RelativePath] = users = new SortedSet<string>(StringComparer.Ordinal);
                }

                _ = users.Add(TypeShape.SimpleName(TypeShape.Outermost(type)) + " → " + TypeShape.SimpleName(referenced));
            }
        }

        return (crossings, usedNodes);
    }

    private static bool IsCompilerHelper(Type type)
    {
        var outermost = TypeShape.Outermost(type);
        return outermost.Namespace is null && outermost.Name.Contains('<', StringComparison.Ordinal);
    }
}
