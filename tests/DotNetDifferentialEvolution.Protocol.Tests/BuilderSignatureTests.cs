using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>Checks the documented CPU builder method against its assembly without changing the shared API.md.</summary>
public sealed class BuilderSignatureTests
{
    private const string Correct = "IDifferentialEvolutionBuilder WithSeed(int seed)";

    /// <summary>The signature of the staged builder matches its compiled overload.</summary>
    [Fact]
    public void AcceptsWithSeedSignature() => Assert.Empty(Problems(Correct));

    /// <summary>Coverage still names the exported type when its example omits a member.</summary>
    [Fact]
    public void BuilderContractMayOmitWithSeed()
    {
        const string document = "## Builder ✅\n```cs\npublic interface IDifferentialEvolutionBuilder { DifferentialEvolution Build(); }\n```";
        Assert.True(ApiDeclarations.NamesType(document, "IDifferentialEvolutionBuilder"));
        var (block, _, startLine) = Assert.Single(ApiDeclarations.ImplementedSections(document));
        Assert.Empty(Declaration.Problems(block, "DotNetDifferentialEvolution", "API.md", startLine));
    }

    /// <summary>Each mutation keeps the owner and method name, and must identify the offending line and compiled overload.</summary>
    [Theory]
    [InlineData("void WithSeed(int seed)")]
    [InlineData("IDifferentialEvolutionBuilder WithSeed(long seed)")]
    [InlineData("IDifferentialEvolutionBuilder WithSeed(int value)")]
    [InlineData("IDifferentialEvolutionBuilder WithSeed()")]
    [InlineData("IDifferentialEvolutionBuilder WithSeed(int seed = 0)")]
    public void RejectsWithSeedSignatureMutation(string signature)
    {
        var message = Assert.Single(Problems(signature));
        Assert.StartsWith("API.md:4:", message, StringComparison.Ordinal);
        Assert.Contains(signature, message, StringComparison.Ordinal);
        Assert.Contains(Correct, message, StringComparison.Ordinal);
    }

    private static IEnumerable<string> Problems(string signature)
    {
        var document = $"## Builder ✅\n```cs\npublic interface IDifferentialEvolutionBuilder {{\n    {signature};\n}}\n```";
        var (block, _, startLine) = Assert.Single(ApiDeclarations.ImplementedSections(document));
        return Declaration.Problems(block, "DotNetDifferentialEvolution", "API.md", startLine);
    }
}
