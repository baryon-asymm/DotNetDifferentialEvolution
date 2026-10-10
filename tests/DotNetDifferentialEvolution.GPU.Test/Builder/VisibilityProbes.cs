using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// An internal class holding a struct of every accessibility, for the known answers of check A15; the types are fetched by name
/// (<see cref="Nested"/>). The test assembly declares <c>InternalsVisibleTo("ILGPURuntime")</c>, so this class and its internal
/// types are visible and its private and protected nested types are not.
/// </summary>
internal class TypesOfEveryAccessibility
{
    /// <summary>Gets the nested type called <paramref name="name"/>, whatever its accessibility.</summary>
    /// <param name="name">The name of the nested type.</param>
    /// <returns>The type.</returns>
    internal static Type Nested(string name) =>
        typeof(TypesOfEveryAccessibility).GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic)!;

    /// <summary>An internal struct nested in this class.</summary>
    /// <param name="Value">A value.</param>
    internal readonly record struct InternalInside(int Value);

    /// <summary>A protected internal struct nested in this class.</summary>
    /// <param name="Value">A value.</param>
    protected internal readonly record struct ProtectedInternalInside(int Value);

    /// <summary>A protected struct nested in this class.</summary>
    /// <param name="Value">A value.</param>
    protected readonly record struct ProtectedInside(int Value);

    /// <summary>A private protected struct nested in this class.</summary>
    /// <param name="Value">A value.</param>
    private protected readonly record struct PrivateProtectedInside(int Value);

    /// <summary>A private struct nested in this class.</summary>
    /// <param name="Value">A value.</param>
    private readonly record struct PrivateInside(int Value);
}

/// <summary>
/// Types in assemblies of the test's own making (dynamic ones), so that check A15 can see an internal type of an assembly that
/// does not grant <c>ILGPURuntime</c> access: the test assembly grants it.
/// </summary>
internal static class DynamicTypes
{
    /// <summary>Makes an internal struct in a new assembly that declares one <c>InternalsVisibleTo</c> per entry of <paramref name="grants"/>.</summary>
    /// <param name="grants">The assembly names the new assembly grants internal access to.</param>
    /// <returns>The internal struct.</returns>
    internal static Type InternalStruct(params string[] grants)
    {
        var assembly = NewAssembly(grants);
        var module = assembly.DefineDynamicModule(assembly.GetName().Name!);
        var internalStruct = module.DefineType(
            "Probe.InternalStruct",
            TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            typeof(ValueType));
        return internalStruct.CreateType();
    }

    /// <summary>Makes a public struct nested in an internal class of a new assembly that declares one <c>InternalsVisibleTo</c> per entry of <paramref name="grants"/>.</summary>
    /// <param name="grants">The assembly names the new assembly grants internal access to.</param>
    /// <returns>The public struct, whose enclosing class is internal.</returns>
    internal static Type PublicStructInAnInternalClass(params string[] grants)
    {
        var assembly = NewAssembly(grants);
        var module = assembly.DefineDynamicModule(assembly.GetName().Name!);
        var holder = module.DefineType("Probe.InternalHolder", TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Abstract);
        var inside = holder.DefineNestedType(
            "PublicStruct",
            TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            typeof(ValueType));
        _ = holder.CreateType();
        return inside.CreateType();
    }

    /// <summary>Makes a public struct nested in a private class nested in a public class of a new assembly that grants nothing.</summary>
    /// <returns>The public struct, whose enclosing class is private.</returns>
    internal static Type PublicStructInAPrivateClass()
    {
        var assembly = NewAssembly([]);
        var module = assembly.DefineDynamicModule(assembly.GetName().Name!);
        var outer = module.DefineType("Probe.Outer", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
        var hidden = outer.DefineNestedType("Hidden", TypeAttributes.NestedPrivate | TypeAttributes.Sealed | TypeAttributes.Abstract);
        var inside = hidden.DefineNestedType(
            "PublicStruct",
            TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            typeof(ValueType));
        _ = outer.CreateType();
        _ = hidden.CreateType();
        return inside.CreateType();
    }

    private static AssemblyBuilder NewAssembly(string[] grants)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Probe" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var grant = typeof(InternalsVisibleToAttribute).GetConstructor([typeof(string)])!;
        foreach (var name in grants)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(grant, [name]));
        }

        return assembly;
    }
}
