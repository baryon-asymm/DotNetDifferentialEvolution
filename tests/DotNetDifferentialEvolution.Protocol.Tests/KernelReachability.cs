using System.Reflection;
using System.Reflection.Emit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The methods reachable from the GPU package's kernel entry points, walked through the tree's own assemblies only: a
/// call into the BCL or ILGPU carries no method body this walk reads, and stops there. Read by the kernel guards of the
/// package's ACCEPTANCE.md, v1 checks 8a (no <c>throw</c>, <c>newarr</c>, <c>newobj</c> of a reference type or
/// <c>box</c>) and 8b (only allow-listed <c>Math</c> and <c>Double</c> members).
/// <para>Adapted from <c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>tests/Protocol.Tests/KernelReachability.cs</c>. Changed here (this node's BOOT.md, Deviations from the kit):</para>
/// <list type="bullet">
/// <item>The entry points are every static method of the package's assembly whose first parameter is
/// <c>ILGPU.Index1D</c>, not the methods of one registry type.</item>
/// <item>Every reached method is read as its generic definition (<see cref="Definition"/>), so that the tokens of a
/// generic method's body resolve against its own type parameters: a call such as <c>DrawOther&lt;TDraws&gt;</c> inside
/// <c>BuildTrial&lt;TDraws&gt;</c> is followed instead of failing to resolve.</item>
/// <item>A call to an interface method of the tree (a constrained call on a struct type parameter resolves to the
/// interface method, which has no body) is followed into every implementation in the interface's own assembly
/// (<see cref="Implementations"/>): <c>IDrawSource.NextIndex</c> reaches <c>PhiloxDraws.NextIndex</c>.</item>
/// <item>A call token the walk cannot resolve is itself a problem (<see cref="UnresolvedCalls"/>): it would be an edge
/// the walk silently skips.</item>
/// <item>The walk returns the reached methods, and the two scans read them, so 8a and 8b share one walk.</item>
/// </list>
/// </summary>
internal static class KernelReachability
{
    private const string IndexTypeName = "ILGPU.Index1D";

    /// <summary>The kernel entry points: every static method of the package's assembly whose first parameter is an
    /// <c>ILGPU.Index1D</c>, ordered by name.</summary>
    public static IReadOnlyList<MethodBase> EntryPoints() =>
        [.. GpuPackage.Assembly.GetTypes()
            .SelectMany(TypeShape.MethodsOf)
            .Where(IsEntryPoint)
            .OrderBy(Name, StringComparer.Ordinal)];

    /// <summary>Every method reached from an entry point, each once, with the first entry point that reached it. The
    /// entry points themselves come first in their walks.</summary>
    public static IReadOnlyList<(MethodBase Entry, MethodBase Method)> Reach()
    {
        var visited = new HashSet<MethodBase>();
        var reached = new List<(MethodBase Entry, MethodBase Method)>();
        foreach (var entry in EntryPoints())
        {
            var pending = new Stack<MethodBase>();
            pending.Push(Definition(entry));
            while (pending.TryPop(out var method))
            {
                if (!visited.Add(method))
                {
                    continue;
                }

                reached.Add((entry, method));
                foreach (var called in Callees(method))
                {
                    pending.Push(called);
                }
            }
        }

        return reached;
    }

    /// <summary>Every <c>throw</c>, <c>newarr</c>, <c>newobj</c> of a reference type and <c>box</c> in the reached
    /// methods, one problem per instruction (check 8a).</summary>
    public static IEnumerable<string> ForbiddenInstructions(IEnumerable<(MethodBase Entry, MethodBase Method)> reached)
    {
        ArgumentNullException.ThrowIfNull(reached);
        foreach (var (entry, method) in reached)
        {
            foreach (var instruction in IlBody.Instructions(method))
            {
                if (ForbiddenInstruction(instruction) is { } what)
                {
                    yield return $"{Where(entry, method)}: {what}";
                }
            }
        }
    }

    /// <summary>Every call the reached methods make to a member of <c>System.Math</c> or <c>System.Double</c>, with the
    /// place it is made (check 8b).</summary>
    public static IEnumerable<(string Where, MethodBase Callee)> MathCalls(IEnumerable<(MethodBase Entry, MethodBase Method)> reached)
    {
        ArgumentNullException.ThrowIfNull(reached);
        foreach (var (entry, method) in reached)
        {
            foreach (var instruction in IlBody.Instructions(method))
            {
                if (instruction.Operand is MethodBase { DeclaringType: { } declaring } called && (declaring == typeof(Math) || declaring == typeof(double)))
                {
                    yield return (Where(entry, method), called);
                }
            }
        }
    }

    /// <summary>Every call instruction of the reached methods whose method token did not resolve: an edge the walk
    /// would otherwise skip without a word.</summary>
    public static IEnumerable<string> UnresolvedCalls(IEnumerable<(MethodBase Entry, MethodBase Method)> reached)
    {
        ArgumentNullException.ThrowIfNull(reached);
        foreach (var (entry, method) in reached)
        {
            foreach (var instruction in IlBody.Instructions(method))
            {
                if (instruction.Code.OperandType == OperandType.InlineMethod && instruction.Operand is null)
                {
                    yield return $"{Where(entry, method)}: {instruction.Code.Name} names a method the walk cannot resolve";
                }
            }
        }
    }

    /// <summary>A method named <c>Type.Method</c>, for messages and ordering.</summary>
    public static string Name(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return $"{method.DeclaringType?.FullName}.{method.Name}";
    }

    private static bool IsEntryPoint(MethodBase method) =>
        method.IsStatic && method.GetParameters() is [var first, ..] && first.ParameterType.FullName == IndexTypeName;

    /// <summary>The methods of the tree an instruction stream calls, as generic definitions: a method with a body is
    /// itself, an interface method is every implementation in its own assembly (and itself, when it has a default
    /// body). A call into the BCL or ILGPU is left alone.</summary>
    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        foreach (var instruction in IlBody.Instructions(method))
        {
            if (instruction.Operand is not MethodBase { DeclaringType: { } declaring } called || NodeAssemblies.NodeOf(declaring) is null)
            {
                continue;
            }

            if (!called.IsAbstract)
            {
                yield return Definition(called);
            }

            if (declaring.IsInterface)
            {
                foreach (var implementation in Implementations(called))
                {
                    yield return implementation;
                }
            }
        }
    }

    /// <summary>Every method that implements an interface method, in the types of the interface's own assembly.</summary>
    private static IEnumerable<MethodBase> Implementations(MethodBase interfaceMethod)
    {
        var contract = interfaceMethod.DeclaringType!;
        foreach (var type in contract.Assembly.GetTypes().Where(type => !type.IsInterface))
        {
            foreach (var implemented in type.GetInterfaces().Where(candidate => SameDefinition(candidate, contract)))
            {
                var map = type.GetInterfaceMap(implemented);
                for (var k = 0; k < map.InterfaceMethods.Length; k++)
                {
                    if (map.InterfaceMethods[k].MetadataToken == interfaceMethod.MetadataToken)
                    {
                        yield return Definition(map.TargetMethods[k]);
                    }
                }
            }
        }
    }

    private static bool SameDefinition(Type left, Type right) =>
        left == right || (left.IsGenericType && right.IsGenericType && left.GetGenericTypeDefinition() == right.GetGenericTypeDefinition());

    /// <summary>The definition behind a method: an instantiation of a generic method, or a method of an instantiated
    /// generic type, resolved back to the method its metadata token names, so its body's tokens resolve against its own
    /// type parameters. For a generic method of a value type, .NET 8's <c>ResolveMethod</c> answers with a method that
    /// is generic but not the definition (measured 2026-10-05 on the parameter rules of the GPU package, which then
    /// resolved none of their calls); that one is taken back to its definition too.</summary>
    private static MethodBase Definition(MethodBase method)
    {
        var resolved = method.Module.ResolveMethod(method.MetadataToken) ?? method;
        return resolved is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } instantiated
            ? instantiated.GetGenericMethodDefinition()
            : resolved;
    }

    private static string? ForbiddenInstruction(Instruction instruction) =>
        instruction.Code == OpCodes.Throw || instruction.Code == OpCodes.Rethrow ? $"throws ({instruction.Code.Name})"
        : instruction.Code == OpCodes.Newarr ? "allocates an array (newarr)"
        : instruction.Code == OpCodes.Box ? "boxes a value (box)"
        : instruction.Code == OpCodes.Newobj && instruction.Operand is MethodBase { DeclaringType: { IsValueType: false } allocated }
            ? $"allocates a reference type ({allocated.FullName}, newobj)"
            : null;

    private static string Where(MethodBase entry, MethodBase method) => $"{Name(method)}, reached from {Name(entry)}";
}
