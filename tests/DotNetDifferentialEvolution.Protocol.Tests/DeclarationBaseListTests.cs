using Xunit;

namespace DotNetDifferentialEvolution.Protocol.Tests;

/// <summary>Checks explicit base-list promises against the built CPU and GPU assemblies.</summary>
public sealed class DeclarationBaseListTests
{
    /// <summary>A document can omit bases, show a subset, or name a transitive base or interface.</summary>
    [Theory]
    [InlineData("DotNetDifferentialEvolution", "public class DifferentialEvolution {}")]
    [InlineData("DotNetDifferentialEvolution", "public class DifferentialEvolution : IDisposable {}")]
    [InlineData("DotNetDifferentialEvolution", "public class DifferentialEvolutionBuilder : IBoundsRequired {} public interface IBoundsRequired {}")]
    [InlineData("DotNetDifferentialEvolution.Algorithms.Lshade", "public class LShadeStrategy : AdaptiveStrategyBase, IGenerationStrategy {}")]
    [InlineData("DotNetDifferentialEvolution.GPU.Objectives", "public readonly struct PointView<T> : IEquatable<PointView<T>> where T : unmanaged {}")]
    public void AcceptsDocumentedBases(string owner, string block) =>
        Assert.Empty(Declaration.Problems(block, owner, "API.md", 89));

    /// <summary>Each error identifies the type, its documented base, and the source location.</summary>
    [Theory]
    [InlineData("DotNetDifferentialEvolution", "public class DifferentialEvolution : IDisposable, IAsyncDisposable {}", "DifferentialEvolution", "IAsyncDisposable")]
    [InlineData("DotNetDifferentialEvolution", "public class DifferentialEvolution : Exception {}", "DifferentialEvolution", "Exception")]
    [InlineData("DotNetDifferentialEvolution.GPU.Objectives", "public readonly struct GeneView : IEquatable<int> {}", "GeneView", "IEquatable<int>")]
    public void RejectsAnAbsentBase(string owner, string block, string type, string absent)
    {
        var problem = Assert.Single(Declaration.Problems(block, owner, "API.md", 89));
        Assert.StartsWith("API.md:89:", problem, StringComparison.Ordinal);
        Assert.Contains(type, problem, StringComparison.Ordinal);
        Assert.Contains(absent, problem, StringComparison.Ordinal);
    }
}
