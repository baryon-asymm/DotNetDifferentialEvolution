using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ProtocolChecks;

/// <summary>
/// What a type names: in its declarations (base type, interfaces, members) and in the bodies of its methods (via
/// <see cref="IlBody"/>), its outermost declaring type, and what the compiler generated rather than the author.
/// </summary>
internal static class TypeShape
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The name without the generic arity suffix.</summary>
    public static string SimpleName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var name = type.Name;
        var arity = name.IndexOf('`', StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }

    /// <summary>The type that holds a nested or compiler-generated type, up to the top level.</summary>
    public static Type Outermost(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    /// <summary>
    /// Written by the compiler or a source generator, not by the author. A type, field or property counts only when its
    /// own name contains <c>&lt;</c>, the one character no identifier the author writes can hold, and carries a
    /// generated-code attribute: a hand-written <c>[CompilerGenerated]</c> on an ordinarily named type no longer exempts
    /// it. A method, constructor or event keeps the attribute-only test: a record's synthesized <c>Equals</c>,
    /// <c>GetHashCode</c>, <c>ToString</c>, <c>PrintMembers</c> and equality operators are methods carrying
    /// <c>[CompilerGenerated]</c> under their ordinary names, and every check that reads members must skip them.
    /// </summary>
    public static bool IsCompilerGenerated(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return (member is not (Type or FieldInfo or PropertyInfo) || member.Name.Contains('<', StringComparison.Ordinal))
            && member.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name is "CompilerGeneratedAttribute" or "EmbeddedAttribute" or "GeneratedCodeAttribute");
    }

    /// <summary>
    /// A type the compiler embeds into an assembly on its own (<c>Microsoft.CodeAnalysis.EmbeddedAttribute</c>,
    /// <c>NullableAttribute</c>, <c>RefSafetyRulesAttribute</c> and the like): it carries both
    /// <c>[CompilerGenerated]</c> and <c>[Embedded]</c>. An author cannot apply <c>[Embedded]</c> outside the
    /// compiler's own namespace without declaring that attribute type, which this test would then flag.
    /// </summary>
    public static bool IsEmbeddedByCompiler(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var names = type.GetCustomAttributesData().Select(attribute => attribute.AttributeType.FullName).ToHashSet(StringComparer.Ordinal);
        return names.Contains("Microsoft.CodeAnalysis.EmbeddedAttribute") && names.Contains("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }

    /// <summary>Every method body a type owns: methods, constructors and the type initializer, declared on the type itself.</summary>
    public static IEnumerable<MethodBase> MethodsOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var method in type.GetMethods(Declared))
        {
            yield return method;
        }

        foreach (var constructor in type.GetConstructors(Declared))
        {
            yield return constructor;
        }

        if (type.TypeInitializer is { } initializer)
        {
            yield return initializer;
        }
    }

    /// <summary>
    /// The types in the shape of a type, each with the place it appears: the base type, the interfaces, the types of its
    /// fields, properties, events, method returns and parameters, the locals of its method bodies, and the constraints
    /// of its own and its methods' generic parameters. Not unwrapped. (The constraints are this tree's addition to the
    /// kit: <c>where T : IRandomGenerator</c> is a dependency the shape walk missed; BOOT.md, ## Deviations from the kit.)
    /// </summary>
    public static IEnumerable<(string Where, Type Type)> Shape(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.BaseType is { } baseType)
        {
            yield return ("base type", baseType);
        }

        foreach (var contract in type.GetInterfaces())
        {
            yield return ("interface", contract);
        }

        if (type.IsGenericTypeDefinition)
        {
            foreach (var parameter in type.GetGenericArguments())
            {
                foreach (var constraint in parameter.GetGenericParameterConstraints())
                {
                    yield return ($"constraint on {parameter.Name}", constraint);
                }
            }
        }

        foreach (var field in type.GetFields(Declared))
        {
            yield return ($"field {field.Name}", field.FieldType);
        }

        foreach (var property in type.GetProperties(Declared))
        {
            yield return ($"property {property.Name}", property.PropertyType);
        }

        foreach (var @event in type.GetEvents(Declared))
        {
            if (@event.EventHandlerType is { } handler)
            {
                yield return ($"event {@event.Name}", handler);
            }
        }

        foreach (var method in MethodsOf(type))
        {
            if (method is MethodInfo info)
            {
                yield return ($"{method.Name} returns", info.ReturnType);
            }

            foreach (var parameter in method.GetParameters())
            {
                yield return ($"{method.Name}({parameter.Name})", parameter.ParameterType);
            }

            foreach (var local in IlBody.Locals(method))
            {
                yield return ($"{method.Name} local {local.LocalIndex}", local.LocalType);
            }

            if (method.IsGenericMethodDefinition)
            {
                foreach (var parameter in method.GetGenericArguments())
                {
                    foreach (var constraint in parameter.GetGenericParameterConstraints())
                    {
                        yield return ($"{method.Name} constraint on {parameter.Name}", constraint);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Every type the given type mentions, in its shape and in the bodies of its methods (<see cref="IlBody.BoundTypes"/>),
    /// unwrapped from arrays, references, pointers and generic arguments (<see cref="Unwrap"/>); generic parameters
    /// dropped. Types from any assembly: the callers keep the ones they care about.
    /// </summary>
    public static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var seen = new HashSet<Type>();
        var candidates = Shape(type).Select(pair => pair.Type).Concat(MethodsOf(type).SelectMany(IlBody.BoundTypes));
        foreach (var candidate in candidates)
        {
            foreach (var unwrapped in Unwrap(candidate))
            {
                if (!unwrapped.IsGenericParameter && seen.Add(unwrapped))
                {
                    yield return unwrapped;
                }
            }
        }
    }

    /// <summary>A type stripped of every layer of arrays, references and pointers (a <c>T[]&amp;</c> down to <c>T</c>,
    /// not to <c>T[]</c>), followed by the same unwrapping of each of its generic arguments, recursively.</summary>
    public static IEnumerable<Type> Unwrap(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var bare = type;
        while ((bare.IsByRef || bare.IsArray || bare.IsPointer) && bare.GetElementType() is { } element)
        {
            bare = element;
        }

        yield return bare;
        if (!bare.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in bare.GetGenericArguments())
        {
            foreach (var nested in Unwrap(argument))
            {
                yield return nested;
            }
        }
    }
}
