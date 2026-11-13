using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>Checks documented type and method constraints using the compiled GPU builder.</summary>
public sealed class DeclarationConstraintTests
{
    private const string Owner = "DotNetDifferentialEvolution.GPU";
    private const string Builder = "public interface IGpuBoundsRequired<TFunction>";
    private const string Method = "public static class GpuDifferentialEvolutionBuilder {" +
        " public static IGpuBoundsRequired<TFunction> ForFunction<TFunction>(TFunction function)";

    /// <summary>An explicit constraint agrees with the assembly. A header can omit its constraint clause.</summary>
    [Fact]
    public void AcceptsKnownConstraints()
    {
        Assert.Empty(Declaration.Problems(Builder + " where TFunction : struct {}", Owner, "API.md", 17));
        Assert.Empty(Declaration.Problems(Builder + " {}", Owner, "API.md", 17));
        Assert.Empty(Declaration.Problems("public interface IGpuPointwiseFitnessFunction<TPoint> where TPoint : unmanaged {}",
            Owner + ".Objectives", "API.md", 58));
        Assert.Empty(Declaration.Problems(Method + " where TFunction : struct, IGpuFitnessFunction; }", Owner, "API.md", 17));
    }

    /// <summary>A constraint clause promises the actual requirements on that parameter.</summary>
    [Theory]
    [InlineData("class")]
    [InlineData("unmanaged")]
    [InlineData("struct, IDisposable")]
    public void RejectsChangedTypeConstraint(string constraint)
    {
        var problem = Assert.Single(Declaration.Problems(Builder + " where TFunction : " + constraint + " {}", Owner, "API.md", 17));
        Assert.Contains("IGpuBoundsRequired", problem, StringComparison.Ordinal);
        Assert.Contains("struct", problem, StringComparison.Ordinal);
        Assert.Contains("TFunction", problem, StringComparison.Ordinal);
    }

    /// <summary>The method signature includes its interface constraint.</summary>
    [Fact]
    public void RejectsChangedMethodConstraint() =>
        Assert.Contains("ForFunction", Assert.Single(Declaration.Problems(Method + " where TFunction : struct; }", Owner, "API.md", 17)), StringComparison.Ordinal);
}
