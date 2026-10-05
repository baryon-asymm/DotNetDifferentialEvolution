using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// The grammar <see cref="ApiDeclarations"/> reads, on documents written here rather than read from the tree, so each
/// rule the tree facts rely on has been seen to hold, and to fail, on a known input (AGENTS.md §13).
/// </summary>
public sealed class ApiDeclarationsTests
{
    private const string Fence = "```";

    /// <summary>The nearest mark above a block decides it; a mark-like character inside the code does not; a document
    /// without marks is ✅ throughout.</summary>
    [Fact]
    public void TheNearestMarkAboveABlockDecidesIt()
    {
        var document = string.Join('\n',
            "## Done ✅", Fence + "csharp", "public sealed class Done; // ⏳ in a comment", Fence,
            "## Planned ⏳", Fence + "csharp", "public sealed class Planned;", Fence);
        var blocks = ApiDeclarations.ImplementedCsharpBlocks(document).ToList();
        var single = Assert.Single(blocks);
        Assert.Contains("Done", single, StringComparison.Ordinal);
        _ = Assert.Single(ApiDeclarations.ImplementedCsharpBlocks(Fence + "csharp\npublic class Unmarked;\n" + Fence));
        Assert.True(ApiDeclarations.NamesType(document, "Done"));
        Assert.False(ApiDeclarations.NamesType(document, "Planned"));
    }

    /// <summary>A record closed by a semicolon owns its positional parameters and nothing after it; a one-line enum owns
    /// nothing after it; a type whose brace opens on the next line owns what follows.</summary>
    [Fact]
    public void ABodilessTypeDoesNotSwallowTheMembersAfterIt()
    {
        var block = string.Join('\n',
            "public static class Outer",
            "{",
            "    public readonly record struct Pair(int Left, int Right);",
            "    public enum Side { Left, Right }",
            "    public static int Sum(Pair pair);",
            "}");
        var declarations = ApiDeclarations.Declarations(block).ToList();
        Assert.True(declarations.Single(d => d.Name == "Outer").OpensBody);
        Assert.False(declarations.Single(d => d.Name == "Pair").OpensBody);
        Assert.False(declarations.Single(d => d.Name == "Side").OpensBody);
        Assert.True(declarations.Single(d => d.Name == "Left").FromTypeLine);
        Assert.False(declarations.Single(d => d.Name == "Sum").FromTypeLine);
    }

    /// <summary>A method returning a tuple is read by its own name, not as a member named after a modifier.</summary>
    [Fact]
    public void AModifierBeforeATupleIsNotAMemberName()
    {
        var declarations = ApiDeclarations.Declarations("internal static (long Low, long High) Band(int count);").Select(d => d.Name).ToList();
        Assert.DoesNotContain("static", declarations);
        Assert.Contains("Band", declarations);
    }

    /// <summary>A tuple return type or a parameter list split across lines is read by the method's own name, and nothing
    /// in its parameters is read as a declaration: the parameters are followed from the name's own parenthesis. The
    /// fourth and fifth cases (a tuple closing on the name's line, parameters below; the fifth with an attribute on the
    /// continuation line) were red before 2026-10-02: `p` was read as a field and `NotNullWhen` as a member (found by
    /// the CPM orchestrator, who ran both parsers side by side). The sixth, an attribute without a tuple, and `new`
    /// before a tuple (CPM's reverse case) were green before and stay as positive controls.</summary>
    [Theory]
    [InlineData("internal static (long Low,\n    long High) Band(int count);")]
    [InlineData("internal static (long Low,\n    long High)\n    Band(\n        int count);")]
    [InlineData("public static int Band(\n    int count,\n    int width);")]
    [InlineData("public static (int Low,\n    int High) Band(\n        int count,\n        double p = 0.5);")]
    [InlineData("public static (int Low,\n    int High) Band(int count,\n        int n, [NotNullWhen(true)] out string? why);")]
    [InlineData("public static bool Band(int count,\n    int n, [NotNullWhen(true)] out string? why);")]
    [InlineData("public new (int A, int B) Band(int count);")]
    public void ADeclarationSplitAcrossLinesIsReadByItsMemberName(string declaration)
    {
        var names = ApiDeclarations.Declarations(declaration + "\npublic static int After();").Select(d => d.Name).ToList();
        Assert.Equal(["Band", "After"], names);
    }

    /// <summary>A section is a tree contract only when its own heading carries the marker; the next heading resets it.</summary>
    [Fact]
    public void OnlyAMarkedHeadingOpensATreeContract()
    {
        var document = string.Join('\n',
            "## Friends " + ProtocolConfig.TreeContractMarker + " ✅", Fence + "csharp", "internal sealed class Shared;", Fence,
            "## Public ✅", Fence + "csharp", "public sealed class Open;", Fence);
        var sections = ApiDeclarations.DeclaredTypeSections(document).ToList();
        Assert.Contains(("Shared", true), sections);
        Assert.Contains(("Open", false), sections);
        Assert.True(ApiDeclarations.NamesTypeInTreeContract(document, "Shared"));
        Assert.False(ApiDeclarations.NamesTypeInPackageSurface(document, "Shared"));
    }
}
