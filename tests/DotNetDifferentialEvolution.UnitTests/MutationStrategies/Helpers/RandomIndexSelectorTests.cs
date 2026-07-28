using DotNetDifferentialEvolution.MutationStrategies.Helpers;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.MutationStrategies.Helpers;

/// <summary>
/// Tests the distinct-index selector that mutation strategies use to pick difference vectors.
/// The exact-value cases use a <see cref="ScriptedRandomProvider"/>; the invariant cases use a
/// seeded provider and assert the guarantees (distinct, in range, never the excluded index).
/// </summary>
[Trait("Category", "Unit")]
public class RandomIndexSelectorTests
{
    [Fact]
    public void ShiftsCandidatesPastExcludedIndex()
    {
        // populationSize 5, exclude 2 → Next(4) draws in [0,3] map to {0,1,3,4} (2 is skipped).
        var random = new ScriptedRandomProvider(ints: [0, 3, 1]);
        Span<int> indices = stackalloc int[3];

        RandomIndexSelector.FillDistinctIndices(indices, populationSize: 5, excludeIndex: 2, new ProviderRandomSource(random));

        Assert.Equal(new[] { 0, 4, 1 }, indices.ToArray());
    }

    [Fact]
    public void RetriesUntilCandidateIsDistinct()
    {
        // populationSize 4, exclude 0 → Next(3) draws in [0,2] map to {1,2,3}.
        // First slot draws 0 → 1. Second slot draws 0 → 1 (collision) → retries, draws 1 → 2.
        var random = new ScriptedRandomProvider(ints: [0, 0, 1]);
        Span<int> indices = stackalloc int[2];

        RandomIndexSelector.FillDistinctIndices(indices, populationSize: 4, excludeIndex: 0, new ProviderRandomSource(random));

        Assert.Equal(new[] { 1, 2 }, indices.ToArray());
    }

    [Theory]
    [InlineData(10, 0, 3)]
    [InlineData(10, 9, 3)]
    [InlineData(50, 25, 5)]
    [InlineData(6, 3, 5)]
    public void ProducesDistinctInRangeIndicesNeverEqualToExcluded(
        int populationSize,
        int excludeIndex,
        int count)
    {
        var random = new DeterministicRandomProvider(seed: populationSize + excludeIndex + count);
        Span<int> indices = stackalloc int[count];

        for (int trial = 0; trial < 200; trial++)
        {
            RandomIndexSelector.FillDistinctIndices(indices, populationSize, excludeIndex, new ProviderRandomSource(random));

            var seen = new HashSet<int>();
            foreach (var index in indices)
            {
                Assert.InRange(index, 0, populationSize - 1);
                Assert.NotEqual(excludeIndex, index);
                Assert.True(seen.Add(index), "indices must be mutually distinct");
            }
        }
    }
}
