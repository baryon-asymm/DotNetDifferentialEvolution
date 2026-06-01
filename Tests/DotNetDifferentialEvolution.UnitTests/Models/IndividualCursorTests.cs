using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.UnitTests.Models;

/// <summary>
/// Tests the <see cref="IndividualCursor"/> snapshot semantics.
/// </summary>
[Trait("Category", "Unit")]
public class IndividualCursorTests
{
    [Fact]
    public void Snapshot_PreservesValueAndGenes()
    {
        double[] genes = [1.0, 2.0, 3.0];
        var cursor = new IndividualCursor(4.0, genes);

        var snapshot = cursor.GetSnapshot();

        Assert.Equal(4.0, snapshot.FitnessFunctionValue);
        Assert.Equal(genes, snapshot.Genes.ToArray());
    }

    [Fact]
    public void ShallowSnapshot_SharesGeneStorage()
    {
        double[] genes = [1.0, 2.0, 3.0];
        var cursor = new IndividualCursor(4.0, genes);

        var shallow = cursor.GetSnapshot(deepCopy: false);
        genes[0] = 99.0;

        Assert.Equal(99.0, shallow.Genes.Span[0]); // reflects the mutation
    }

    [Fact]
    public void DeepSnapshot_CopiesGeneStorage()
    {
        double[] genes = [1.0, 2.0, 3.0];
        var cursor = new IndividualCursor(4.0, genes);

        var deep = cursor.GetSnapshot(deepCopy: true);
        genes[0] = 99.0;

        Assert.Equal(1.0, deep.Genes.Span[0]); // isolated from the mutation
    }
}
