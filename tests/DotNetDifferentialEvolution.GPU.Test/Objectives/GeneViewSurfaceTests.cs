using System.Reflection;
using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.Objectives;

/// <summary>
/// ACCEPTANCE.md, check 2a: <see cref="GeneView"/> exposes no writable member, by reflection. Nothing
/// an objective receives can write device memory, which is half of what keeps a generation free of
/// races (BOOT.md, invariant 2); the kernel's own slot discipline is the other half (check 2b).
/// </summary>
[Trait("Category", "Unit")]
public class GeneViewSurfaceTests
{
    private const BindingFlags Everything =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private const BindingFlags Visible = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>No property of any accessibility, the indexer included, has a setter or an <c>init</c> accessor.</summary>
    [Fact]
    public void NoPropertyHasASetter()
    {
        var properties = typeof(GeneView).GetProperties(Everything);

        Assert.Contains(properties, property => property.GetIndexParameters().Length == 1);
        Assert.All(properties, property => Assert.Null(property.GetSetMethod(nonPublic: true)));
    }

    /// <summary>No method or property of any accessibility returns by reference.</summary>
    [Fact]
    public void NoMemberReturnsByReference()
    {
        Assert.All(typeof(GeneView).GetMethods(Everything), method => Assert.False(method.ReturnType.IsByRef, method.Name));
        Assert.All(typeof(GeneView).GetProperties(Everything), property => Assert.False(property.PropertyType.IsByRef, property.Name));
    }

    /// <summary>No public field, instance or static.</summary>
    [Fact]
    public void NoFieldIsPublic() => Assert.Empty(typeof(GeneView).GetFields(Visible));
}
