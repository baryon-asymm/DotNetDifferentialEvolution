namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Check A15 of the GPU package's Kernels ACCEPTANCE.md, the rule: ILGPU's runtime assembly sees a type only when it, every type
/// it is nested in and every generic argument is public, or internal in an assembly that declares
/// <c>[assembly: InternalsVisibleTo("ILGPURuntime")]</c>; a private, protected or private protected nested type is never
/// visible. Each known answer has its own case. The test assembly declares the attribute; the assemblies without it are
/// dynamic ones made for the cases (<see cref="DynamicTypes"/>). The public types are the framework's.
/// </summary>
public class ObjectiveVisibilityTests
{
    private const string Runtime = "ILGPURuntime";

    /// <summary>A public struct is visible.</summary>
    [Fact]
    public void APublicStructIsVisible() => AssertVisible(typeof(DateTime));

    /// <summary>A public enum nested in a public class is visible.</summary>
    [Fact]
    public void APublicTypeNestedInAPublicClassIsVisible() => AssertVisible(typeof(Environment.SpecialFolder));

    /// <summary>An internal struct of an assembly that declares the attribute is visible.</summary>
    [Fact]
    public void AnInternalStructWithTheAttributeIsVisible() => AssertVisible(typeof(Sphere));

    /// <summary>An internal struct nested in an internal class, in an assembly that declares the attribute, is visible.</summary>
    [Fact]
    public void AnInternalNestedStructWithTheAttributeIsVisible() => AssertVisible(Nested("InternalInside"));

    /// <summary>A protected internal struct in an assembly that declares the attribute is visible: its internal half is reachable.</summary>
    [Fact]
    public void AProtectedInternalStructWithTheAttributeIsVisible() => AssertVisible(Nested("ProtectedInternalInside"));

    /// <summary>An internal struct of an assembly that declares no attribute is not visible, and is the part named.</summary>
    [Fact]
    public void AnInternalStructWithoutTheAttributeIsNotVisible() => AssertInvisible(DynamicTypes.InternalStruct());

    /// <summary>An internal struct of a dynamic assembly that declares the attribute is visible.</summary>
    [Fact]
    public void AnInternalStructOfADynamicAssemblyWithTheAttributeIsVisible() => AssertVisible(DynamicTypes.InternalStruct(Runtime));

    /// <summary>The grant is matched without regard to case, as assembly names are.</summary>
    [Fact]
    public void TheGrantIsMatchedWithoutRegardToCase() => AssertVisible(DynamicTypes.InternalStruct("ilgpuruntime"));

    /// <summary>A grant to another assembly does not make an internal struct visible.</summary>
    [Fact]
    public void AGrantToAnotherAssemblyDoesNotMakeAnInternalStructVisible() =>
        AssertInvisible(DynamicTypes.InternalStruct("SomeoneElse", "ILGPURuntimeAndMore"));

    /// <summary>A grant among others makes an internal struct visible.</summary>
    [Fact]
    public void AGrantAmongOthersMakesAnInternalStructVisible() => AssertVisible(DynamicTypes.InternalStruct("SomeoneElse", Runtime));

    /// <summary>A public struct nested in an internal class without the attribute is not visible: its enclosing class is the part named.</summary>
    [Fact]
    public void APublicStructInAnInternalClassWithoutTheAttributeIsNotVisible()
    {
        var inside = DynamicTypes.PublicStructInAnInternalClass();

        AssertInvisible(inside, inside.DeclaringType!);
    }

    /// <summary>A public struct nested in an internal class with the attribute is visible.</summary>
    [Fact]
    public void APublicStructInAnInternalClassWithTheAttributeIsVisible() =>
        AssertVisible(DynamicTypes.PublicStructInAnInternalClass(Runtime));

    /// <summary>A private nested struct is not visible, though the assembly declares the attribute.</summary>
    [Fact]
    public void APrivateNestedStructIsNotVisible() => AssertInvisible(Nested("PrivateInside"));

    /// <summary>A protected nested struct is not visible, though the assembly declares the attribute.</summary>
    [Fact]
    public void AProtectedNestedStructIsNotVisible() => AssertInvisible(Nested("ProtectedInside"));

    /// <summary>A private protected nested struct is not visible, though the assembly declares the attribute.</summary>
    [Fact]
    public void APrivateProtectedNestedStructIsNotVisible() => AssertInvisible(Nested("PrivateProtectedInside"));

    /// <summary>A public struct nested in a private class is not visible: every type it is nested in must be.</summary>
    [Fact]
    public void APublicStructInAPrivateClassIsNotVisible()
    {
        var inside = DynamicTypes.PublicStructInAPrivateClass();

        AssertInvisible(inside, inside.DeclaringType!);
    }

    /// <summary>A public generic struct over a private one is not visible: every generic argument must be, and the argument is the part named.</summary>
    [Fact]
    public void APublicGenericOverAPrivateStructIsNotVisible() =>
        AssertInvisible(typeof(Nullable<>).MakeGenericType(Nested("PrivateInside")), Nested("PrivateInside"));

    /// <summary>A public generic class over an internal struct without the attribute is not visible.</summary>
    [Fact]
    public void APublicGenericOverAnInternalStructWithoutTheAttributeIsNotVisible()
    {
        var argument = DynamicTypes.InternalStruct();

        AssertInvisible(typeof(List<>).MakeGenericType(argument), argument);
    }

    /// <summary>A public generic struct over a public one is visible.</summary>
    [Fact]
    public void APublicGenericOverAPublicStructIsVisible() => AssertVisible(typeof(int?));

    /// <summary>A generic over a generic over a private struct is not visible: the walk goes down each argument.</summary>
    [Fact]
    public void AGenericOverAGenericOverAPrivateStructIsNotVisible()
    {
        var inner = typeof(List<>).MakeGenericType(Nested("PrivateInside"));

        AssertInvisible(typeof(List<>).MakeGenericType(inner), Nested("PrivateInside"));
    }

    /// <summary>An array of a private struct is not visible: the element type is not.</summary>
    [Fact]
    public void AnArrayOfAPrivateStructIsNotVisible() =>
        AssertInvisible(Nested("PrivateInside").MakeArrayType(), Nested("PrivateInside"));

    /// <summary>An open generic type is judged by itself: its parameters are not parts.</summary>
    [Fact]
    public void AnOpenPublicGenericIsVisible() => AssertVisible(typeof(List<>));

    private static Type Nested(string name) => TypesOfEveryAccessibility.Nested(name);

    private static void AssertVisible(Type type)
    {
        Assert.True(ObjectiveVisibility.IsVisible(type));
        Assert.Null(ObjectiveVisibility.FirstInvisiblePart(type));
    }

    private static void AssertInvisible(Type type, Type? part = null)
    {
        Assert.False(ObjectiveVisibility.IsVisible(type));
        Assert.Equal(part ?? type, ObjectiveVisibility.FirstInvisiblePart(type));
    }
}
