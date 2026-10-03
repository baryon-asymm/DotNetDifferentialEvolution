using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ProtocolChecks;

/// <summary>
/// Declarations (AGENTS.md §7, §13): every type and member declared in a C# block of an API.md under ✅ exists in an
/// assembly of the tree, the node's own first. Names, not signatures: the signatures are pinned by the surface
/// snapshot, and a second copy of them here would be a second thing to keep in step. A block under ⏳ is a sketch and
/// is not read; a document without a mark counts as ✅ throughout.
/// </summary>
public sealed class DeclarationTests
{
    /// <summary>Every declaration under a ✅ mark of an API.md exists in the code, the type and the member.</summary>
    [Fact]
    public void EveryDeclarationUnderATickExists()
    {
        var blocks = Tree.Nodes.Sum(node => ApiDeclarations.ImplementedCsharpBlocks(File.ReadAllText(node.Api)).Count());
        Assert.True(blocks > 0, "found nothing: no C# block under ✅ in any API.md, so the walk proves nothing (or the parser lost the documents)");
        var problems = Tree.Nodes.SelectMany(ProblemsOf).ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static IEnumerable<string> ProblemsOf(Node node)
    {
        var api = Tree.Relative(node.Api);
        foreach (var block in ApiDeclarations.ImplementedCsharpBlocks(File.ReadAllText(node.Api)))
        {
            foreach (var problem in ProblemsInBlock(node, api, block))
            {
                yield return problem;
            }
        }
    }

    /// <summary>The problems of one ✅ block: a declared type no assembly has, or a declared member its owner does not
    /// have. A type that opens a body owns what follows it; a bodiless record owns only the positional parameters on its
    /// own line, after which the enclosing type owns the members again. A member named after a type of the block is a
    /// constructor, which reflection reports as <c>.ctor</c>, and is skipped.</summary>
    private static IEnumerable<string> ProblemsInBlock(Node node, string api, string block)
    {
        Type? current = null;
        Type? enclosing = null;
        var typesInBlock = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in ApiDeclarations.Declarations(block))
        {
            if (declaration.IsType)
            {
                _ = typesInBlock.Add(declaration.Name);
                var declared = Find(node, declaration.Name);
                if (declared is null)
                {
                    yield return $"{api}: declares the type {declaration.Name} under ✅, and no assembly of the tree has it";
                }

                current = declared;
                if (declaration.OpensBody)
                {
                    enclosing = declared;
                }

                continue;
            }

            if (!declaration.FromTypeLine)
            {
                current = enclosing ?? current;
            }

            if (current is null || typesInBlock.Contains(declaration.Name) || (declaration.IsEnumMember && !current.IsEnum))
            {
                continue;
            }

            if (!HasMember(current, declaration.Name))
            {
                yield return $"{api}: {TypeShape.SimpleName(current)} has no member named {declaration.Name}, declared under ✅";
            }
        }
    }

    /// <summary>The type of the given simple name: attributed to the node itself first, then anywhere in its own effective
    /// assembly, then in any assembly of the tree.</summary>
    private static Type? Find(Node node, string simpleName)
    {
        var ownAssembly = NodeAssemblies.AssemblyOf(node);
        return NodeAssemblies.Assemblies.Values.Distinct()
            .SelectMany(candidate => candidate.GetTypes())
            .Where(type => TypeShape.SimpleName(type) == simpleName)
            .OrderBy(type => NodeAssemblies.NodeOf(type) == node ? 0 : type.Assembly == ownAssembly ? 1 : 2)
            .ThenBy(type => type.Assembly.GetName().Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool HasMember(Type type, string name)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        return type.GetMember(name, Any).Length > 0 || type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic) is not null;
    }
}
