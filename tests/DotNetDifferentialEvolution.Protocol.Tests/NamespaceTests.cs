using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ProtocolChecks;

/// <summary>
/// Namespace (AGENTS.md §1): every type of every assembly of the tree lives in exactly the namespace of a node, and
/// that node's code compiles into the assembly the type was found in. The reflection checks attribute a type to a
/// node by its namespace, so a type in the wrong namespace is attributed to the wrong node by every other fact. A
/// namespace only a prefix of some node matches, with no node at the exact deeper path, is still misplaced. A
/// deviation declared in <see cref="ProtocolConfig.NamespaceExceptions"/> is honoured and must still be needed.
/// </summary>
public sealed class NamespaceTests
{
    /// <summary>Every type lives in the namespace its node's path gives; every declared exception is still in use.</summary>
    [Fact]
    public void EveryTypeOfEveryAssemblyLivesInTheNamespaceOfItsNode()
    {
        var assemblies = NodeAssemblies.Assemblies.Values.Distinct().OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal).ToList();
        var walked = assemblies.Sum(assembly => Placed(assembly).Count());
        Assert.True(walked > 0, "found nothing: no type with a namespace was found in any assembly of the tree, so the walk proves nothing");

        var seenExceptions = new HashSet<string>(StringComparer.Ordinal);
        var problems = assemblies.SelectMany(assembly => MisplacedTypeProblems(assembly, seenExceptions)).ToList();
        foreach (var stale in ProtocolConfig.NamespaceExceptions.Keys.Except(seenExceptions).Order(StringComparer.Ordinal))
        {
            problems.Add($"{stale} is listed in ProtocolConfig.NamespaceExceptions, and no type of its own assembly uses the exempted " +
                         $"namespace {ProtocolConfig.NamespaceExceptions[stale]} any more: the deviation is lifted, remove the entry " +
                         "(and the declared deviation in that node's BOOT.md)");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The types the fact judges: top-level, with a namespace, written by the author.</summary>
    private static IEnumerable<Type> Placed(Assembly assembly) =>
        assembly.GetTypes().Where(type => !type.IsNested && type.Namespace is not null && !TypeShape.IsCompilerGenerated(type) && !TypeShape.IsEmbeddedByCompiler(type));

    private static IEnumerable<string> MisplacedTypeProblems(Assembly assembly, HashSet<string> seenExceptions)
    {
        foreach (var type in Placed(assembly).OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            var owner = NodeAssemblies.NodeOf(type);
            if (owner is not null && NodeAssemblies.ExemptNamespaceOf(owner) is { } exempt && type.Namespace == exempt && NodeAssemblies.NodeOf(assembly) == owner)
            {
                _ = seenExceptions.Add(owner.RelativePath);
                continue;
            }

            if (owner is null || owner.Namespace != type.Namespace)
            {
                yield return $"{type.FullName} is in namespace {type.Namespace}, and no node of the tree is exactly that namespace " +
                             $"(AGENTS.md §1: the namespace repeats the directory path under {ProtocolConfig.RootNamespace})";
            }
            else if (NodeAssemblies.AssemblyOf(owner) != assembly)
            {
                yield return $"{type.FullName} is in namespace {type.Namespace}; its node {owner.Name} compiles into " +
                             $"{NodeAssemblies.AssemblyOf(owner)?.GetName().Name ?? "no assembly"}, not {assembly.GetName().Name} (AGENTS.md §1)";
            }
        }
    }
}
