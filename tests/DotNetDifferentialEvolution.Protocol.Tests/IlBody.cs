using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ProtocolChecks;

/// <summary>
/// The instructions of a method body, and the types they bind to. Every opcode comes from the runtime's own table
/// (<see cref="OpCodes"/>), not a transcription: a wrong opcode or operand width would desynchronise the walk and
/// yield garbage instead of failing (AGENTS.md §13: dependencies are read from method bodies too). Takes methods,
/// never types: a type's methods are <see cref="TypeShape"/>'s to enumerate, so <c>TypeShape</c> → <c>IlBody</c>
/// stays one-way.
/// </summary>
internal static class IlBody
{
    private static readonly Dictionary<byte, OpCode> OneByte = OpCodeTable(single: true);

    private static readonly Dictionary<byte, OpCode> TwoByte = OpCodeTable(single: false);

    /// <summary>Every <see cref="OperandType"/> whose width does not depend on the instruction stream, keyed by the
    /// enum rather than switched on: a lookup never has to name the obsolete <c>InlinePhi</c> (CS0618) and carries no
    /// unreachable arm. <see cref="OperandType.InlineSwitch"/> is computed from the stream; every type not listed
    /// (the metadata tokens, <c>InlineBrTarget</c>, <c>InlineI</c>, <c>ShortInlineR</c>) is four bytes wide.</summary>
    private static readonly Dictionary<OperandType, int> OperandWidths = new()
    {
        [OperandType.InlineNone] = 0,
        [OperandType.ShortInlineBrTarget] = 1,
        [OperandType.ShortInlineI] = 1,
        [OperandType.ShortInlineVar] = 1,
        [OperandType.InlineVar] = 2,
        [OperandType.InlineI8] = 8,
        [OperandType.InlineR] = 8,
    };

    /// <summary>The instructions of a method body; empty for one the runtime reports no body for.</summary>
    public static IEnumerable<Instruction> Instructions(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var il = Body(method)?.GetILAsByteArray() ?? [];
        var typeContext = method.DeclaringType is { IsGenericTypeDefinition: true } declaring ? declaring.GetGenericArguments() : null;
        var methodContext = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
        var module = method.Module;
        var offset = 0;
        while (offset < il.Length)
        {
            var code = il[offset++];
            OpCode opcode;
            if (code == 0xFE)
            {
                if (offset >= il.Length || !TwoByte.TryGetValue(il[offset++], out opcode))
                {
                    yield break;
                }
            }
            else if (!OneByte.TryGetValue(code, out opcode))
            {
                yield break;
            }

            var width = opcode.OperandType == OperandType.InlineSwitch
                ? 4 + (4 * BitConverter.ToInt32(il, offset))
                : OperandWidths.GetValueOrDefault(opcode.OperandType, 4);

            MemberInfo? operand = null;
            if (opcode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                operand = Resolve(module, BitConverter.ToInt32(il, offset), typeContext, methodContext);
            }

            offset += width;
            yield return new Instruction(opcode, operand);
        }
    }

    /// <summary>
    /// The types a method's body binds to: the members its instructions name, with the types those members' signatures
    /// carry (a called method's return and parameter types, a field's type) and the generic arguments of the methods it
    /// calls. The signatures matter: an enum used only through its literals is an integer in the IL and appears in no
    /// token of its own, yet the method it is passed to or the field it is stored in names it, and that is where the use
    /// is caught.
    /// </summary>
    public static IEnumerable<Type> BoundTypes(MethodBase method)
    {
        foreach (var instruction in Instructions(method))
        {
            foreach (var type in Bound(instruction.Operand))
            {
                yield return type;
            }
        }
    }

    /// <summary>The local variables declared in a method body, empty for one the runtime reports no body for.</summary>
    public static IEnumerable<LocalVariableInfo> Locals(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return Body(method)?.LocalVariables ?? [];
    }

    private static MemberInfo? Resolve(Module module, int token, Type[]? typeContext, Type[]? methodContext)
    {
        try
        {
            return module.ResolveMember(token, typeContext, methodContext);
        }
        catch (ArgumentException)
        {
            // A token this context cannot resolve names nothing attributable to a node; the walk stays in step either way.
            return null;
        }
        catch (InvalidOperationException)
        {
            // A module that does not support token resolution (a dynamic or in-memory one) names nothing either.
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (MissingMemberException)
        {
            // The token no longer resolves to a live member.
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static IEnumerable<Type> Bound(MemberInfo? operand)
    {
        // An if/else chain rather than a switch: Type, MethodBase and FieldInfo are all MemberInfo, so a switch would
        // need a default arm that is unreachable or leave the analyzer reading it as unpopulated.
        if (operand is Type named)
        {
            yield return named;
        }
        else if (operand is MethodBase called)
        {
            foreach (var type in BoundToCall(called))
            {
                yield return type;
            }
        }
        else if (operand is FieldInfo field)
        {
            if (field.DeclaringType is { } holder)
            {
                yield return holder;
            }

            yield return field.FieldType;
        }
        else if (operand?.DeclaringType is { } declaring)
        {
            yield return declaring;
        }
    }

    private static IEnumerable<Type> BoundToCall(MethodBase called)
    {
        if (called.DeclaringType is { } owner)
        {
            yield return owner;
        }

        if (called is MethodInfo info)
        {
            if (info.IsGenericMethod)
            {
                foreach (var argument in info.GetGenericArguments())
                {
                    yield return argument;
                }
            }

            yield return info.ReturnType;
        }

        foreach (var parameter in called.GetParameters())
        {
            yield return parameter.ParameterType;
        }
    }

    private static MethodBody? Body(MethodBase method)
    {
        try
        {
            return method.GetMethodBody();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static Dictionary<byte, OpCode> OpCodeTable(bool single)
    {
        var table = new Dictionary<byte, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opcode)
            {
                continue;
            }

            var value = (ushort)opcode.Value;
            if (single ? value <= 0xFF && opcode.Value != 0xFE : value > 0xFF)
            {
                table[(byte)(value & 0xFF)] = opcode;
            }
        }

        return table;
    }
}
