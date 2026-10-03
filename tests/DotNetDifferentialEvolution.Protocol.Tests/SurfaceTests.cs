using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>
/// Surface (AGENTS.md §2, §13): the public surface of every library assembly of the tree equals the approved snapshot
/// <c>PublicSurface.approved.txt</c>, one section per assembly; test assemblies are left out (their public types are
/// their tests, which change constantly). A tripwire, not a contract: the contract is each node's API.md; the snapshot
/// makes a change of the contract impossible to commit unnoticed, because it moves in the same diff.
/// </summary>
public sealed class SurfaceTests
{
    /// <summary>The public surface of the library assemblies matches the approved snapshot.</summary>
    [Fact]
    public void ThePublicSurfaceOfTheLibraryAssembliesMatchesTheApprovedSnapshot()
    {
        var assemblies = NodeAssemblies.LibraryAssemblies;
        Assert.True(assemblies.Count > 0, "found nothing: no library assembly of the tree was loaded, so the walk proves nothing " +
                                          "(is every node project referenced by the test project?)");
        ApprovedSnapshot.Verify("PublicSurface", "the public surface of the library assemblies", SurfaceText.Describe(assemblies));
    }
}
