using System.Reflection;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The calls to an ILGPU host transfer: a method of an ILGPU assembly named <c>CopyToCPU…</c> or <c>CopyFromCPU…</c>,
/// the <c>…Async</c> and <c>…UnsafeAsync</c> forms included. Read by the GPU package's checks 5a (only one helper
/// transfers) and 8d (host memory crosses pinned).
/// <para>Adapted from <c>AerospacePropellantThermodynamics</c>, commit <c>5fdd82c</c>,
/// <c>tests/Protocol.Tests/InvariantTests.cs</c>: the private helpers <c>IlgpuTransferCalls</c>, <c>IsIlgpuTransfer</c>,
/// <c>IsRawReference</c> and <c>IsSpan</c> of the fact <c>NoSrcMethodPassesHostMemoryToAnIlgpuTransferByReference</c>,
/// moved into a class of their own so that both checks read one definition of a transfer (this node's BOOT.md,
/// Deviations from the kit).</para>
/// </summary>
internal static class IlgpuTransfers
{
    /// <summary>Every call a method body of the assembly makes to an ILGPU host transfer: the calling method and the
    /// transfer it calls. A call from a lambda, local function or state machine is the compiler-generated method's.</summary>
    public static IEnumerable<(MethodBase Method, MethodBase Callee)> Calls(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        foreach (var method in assembly.GetTypes().SelectMany(TypeShape.MethodsOf))
        {
            foreach (var instruction in IlBody.Instructions(method))
            {
                if (instruction.Operand is MethodBase { DeclaringType: { } declaring } callee && IsTransfer(declaring, callee))
                {
                    yield return (method, callee);
                }
            }
        }
    }

    /// <summary>A by-reference parameter that is not a span: a <c>ref T</c> into host memory, which ILGPU 1.5.3 turns
    /// into a raw pointer without pinning it. The <c>in Span&lt;T&gt;</c> and <c>in ReadOnlySpan&lt;T&gt;</c> of the
    /// pinning overloads are by-reference too, and are not refused.</summary>
    public static bool IsRawReference(ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return parameter.ParameterType.IsByRef && !IsSpan(parameter.ParameterType.GetElementType()!);
    }

    /// <summary>A transfer as a message: <c>Type.Method(ParameterType, …)</c>.</summary>
    public static string Describe(MethodBase callee)
    {
        ArgumentNullException.ThrowIfNull(callee);
        return $"{callee.DeclaringType?.Name}.{callee.Name}({string.Join(", ", callee.GetParameters().Select(parameter => parameter.ParameterType.Name))})";
    }

    private static bool IsTransfer(Type declaring, MethodBase callee) =>
        (declaring.Assembly.GetName().Name ?? string.Empty).StartsWith("ILGPU", StringComparison.Ordinal)
        && (callee.Name.StartsWith("CopyToCPU", StringComparison.Ordinal) || callee.Name.StartsWith("CopyFromCPU", StringComparison.Ordinal));

    private static bool IsSpan(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Span<>) || type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));
}
