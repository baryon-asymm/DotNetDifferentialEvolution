using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// Which types ILGPU's runtime assembly can see (ACCEPTANCE.md, A15). ILGPU emits its kernel launchers into a dynamic
/// assembly named <c>ILGPURuntime</c>, and a launcher can name a type only when that type, every type it is nested in and
/// every generic argument is public, or is internal in an assembly that declares
/// <c>[assembly: InternalsVisibleTo("ILGPURuntime")]</c>. A private, protected or private protected nested type is never
/// visible, whatever the assembly declares.
/// </summary>
internal static class ObjectiveVisibility
{
    /// <summary>The name of ILGPU's dynamic assembly.</summary>
    internal const string RuntimeAssemblyName = "ILGPURuntime";

    /// <summary>
    /// Finds the first type of <paramref name="type"/>'s walk that the runtime assembly cannot see: the type itself, then every
    /// type it is nested in, outward, then each generic argument by the same walk.
    /// </summary>
    /// <param name="type">The type a kernel names.</param>
    /// <returns>The first part the runtime assembly cannot see, or <see langword="null"/> when it sees them all.</returns>
    internal static Type? FirstInvisiblePart(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.HasElementType)
        {
            return FirstInvisiblePart(type.GetElementType()!);
        }

        for (var enclosing = type; enclosing is not null; enclosing = enclosing.DeclaringType)
        {
            if (!IsDeclaredVisible(enclosing))
            {
                return enclosing;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            if (!argument.IsGenericParameter && FirstInvisiblePart(argument) is { } part)
            {
                return part;
            }
        }

        return null;
    }

    /// <summary>Gets whether the runtime assembly sees <paramref name="type"/>, by <see cref="FirstInvisiblePart"/>.</summary>
    /// <param name="type">The type a kernel names.</param>
    /// <returns><see langword="true"/> when it sees every part of the type.</returns>
    internal static bool IsVisible(Type type) => FirstInvisiblePart(type) is null;

    /// <summary>Whether one type, taken alone, can be named by the runtime assembly.</summary>
    private static bool IsDeclaredVisible(Type type)
    {
        if (type.IsPublic || type.IsNestedPublic)
        {
            return true;
        }

        // Private, protected and private protected members of a type are out of reach of any other assembly.
        if (type.IsNestedPrivate || type.IsNestedFamily || type.IsNestedFamANDAssem)
        {
            return false;
        }

        // Internal (and protected internal): reachable by an assembly the declaring assembly has named.
        return GrantsRuntimeAssembly(type.Assembly);
    }

    private static bool GrantsRuntimeAssembly(Assembly assembly)
    {
        foreach (var grant in assembly.GetCustomAttributes<InternalsVisibleToAttribute>())
        {
            // The grant must be the bare name: the runtime assembly is not strong-named, so a grant that names a key never matches it.
            if (string.Equals(grant.AssemblyName.Trim(), RuntimeAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
