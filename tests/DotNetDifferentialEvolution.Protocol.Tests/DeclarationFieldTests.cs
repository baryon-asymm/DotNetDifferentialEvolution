using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>Checks the kit's field-signature comparison on fields in this assembly.</summary>
public sealed class DeclarationFieldTests
{
    private static IEnumerable<string> Problems(string field) => Declaration.Problems(
        "internal static class ProtocolConfig { " + field + " }", "DotNetDifferentialEvolution.Protocol.Tests", "API.md", 1);

    /// <summary>Constant values, readonly modifiers and generic field types bind in the compiled node.</summary>
    [Fact]
    public void AcceptsKnownFields()
    {
        Assert.Empty(Problems("public const string RootNamespace = \"\";"));
        Assert.Empty(Problems("public static readonly IReadOnlySet<string> TransparentSegments;"));
    }

    /// <summary>One changed part of the field signature identifies the mismatched member.</summary>
    [Theory]
    [InlineData("public const string RootNamespace = \"unexpected\";", "RootNamespace")]
    [InlineData("public static readonly IReadOnlySet<int> TransparentSegments;", "TransparentSegments")]
    [InlineData("public static IReadOnlySet<string> TransparentSegments;", "TransparentSegments")]
    public void RejectsChangedField(string field, string member) =>
        Assert.Contains(member, Assert.Single(Problems(field)), StringComparison.Ordinal);
}
