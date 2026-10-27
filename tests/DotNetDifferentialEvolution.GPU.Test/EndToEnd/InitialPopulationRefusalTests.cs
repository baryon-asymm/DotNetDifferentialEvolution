namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check I6 of the Kernels node's ACCEPTANCE.md, the refusals of <c>WithInitialBounds</c> and <c>WithInitialPopulation</c>
/// (API.md, the 1.2.0 rows of the errors): <c>WithInitialBounds</c> with D − 1 values, a <c>NaN</c>, an infinity, lower above
/// upper, a value below the search box and one above it; <c>WithInitialPopulation</c> with N·D − 1 values, a <c>NaN</c>, an
/// infinity, a value outside the search box; the box or the genes made unfit by a later <c>WithBounds</c> or
/// <c>WithPopulationSize</c> on a retained stage, and both calls on one builder (<c>Build</c>). Each message names the gene,
/// or the individual and the gene. A value on the search box's edge is accepted. D = 3, N = 8, the search box
/// [−2, 2] × [−1, 1] × [0, 4]. The refusals come before any device is opened; two cases build on the CPU accelerator to show
/// that the values are copied at the call and that a second call replaces the first.
/// </summary>
public class InitialPopulationRefusalTests
{
    private const int GenomeSize = 3;
    private const int PopulationSize = 8;

    private static readonly double[] Lower = [-2.0, -1.0, 0.0];
    private static readonly double[] Upper = [2.0, 1.0, 4.0];

    /// <summary>The initial lower bound with one value fewer than D is an <see cref="ArgumentException"/> from <c>WithInitialBounds</c>.</summary>
    [Fact]
    public void AnInitialLowerBoundOfFewerThanDGenesIsRefused()
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialBounds(Lower.AsMemory(0, 2), Upper));

        Assert.Equal("initialLowerBound", failure.ParamName);
        Assert.Contains("2 values", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The initial upper bound with one value fewer than D is an <see cref="ArgumentException"/> from <c>WithInitialBounds</c>.</summary>
    [Fact]
    public void AnInitialUpperBoundOfFewerThanDGenesIsRefused()
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialBounds(Lower, Upper.AsMemory(0, 2)));

        Assert.Equal("initialUpperBound", failure.ParamName);
        Assert.Contains("2 values", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An initial bound that is <c>NaN</c> or an infinity is refused, naming the gene.</summary>
    /// <param name="value">The value of gene 1's initial lower bound.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void ANonFiniteInitialLowerBoundIsRefused(double value)
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialBounds(Values(-2.0, value, 0.0), Upper));

        Assert.Equal("initialLowerBound", failure.ParamName);
        Assert.Contains("gene 1", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An initial upper bound that is <c>NaN</c> or an infinity is refused, naming the gene.</summary>
    /// <param name="value">The value of gene 2's initial upper bound.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void ANonFiniteInitialUpperBoundIsRefused(double value)
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialBounds(Lower, Values(2.0, 1.0, value)));

        Assert.Equal("initialUpperBound", failure.ParamName);
        Assert.Contains("gene 2", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An initial lower bound above its upper bound is refused, naming the gene.</summary>
    [Fact]
    public void AnInitialLowerBoundAboveItsUpperBoundIsRefused()
    {
        var failure = Assert.Throws<ArgumentException>(
            () => Assembled().Last.WithInitialBounds(Values(-2.0, 0.5, 0.0), Values(2.0, 0.25, 4.0)));

        Assert.Contains("gene 1", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An initial lower bound below the search box is refused, naming the gene.</summary>
    [Fact]
    public void AnInitialBoxBelowTheSearchBoxIsRefused()
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialBounds(Values(-2.0, -1.0, -0.001), Upper));

        Assert.Equal("initialLowerBound", failure.ParamName);
        Assert.Contains("gene 2", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An initial upper bound above the search box is refused, naming the gene.</summary>
    [Fact]
    public void AnInitialBoxAboveTheSearchBoxIsRefused()
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialBounds(Lower, Values(2.0, 1.001, 4.0)));

        Assert.Equal("initialUpperBound", failure.ParamName);
        Assert.Contains("gene 1", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The search box is closed: an initial box on its edges, whole or a point on an edge, is accepted.</summary>
    [Fact]
    public void AnInitialBoxOnTheSearchBoxsEdgeIsAccepted()
    {
        Assert.NotNull(Assembled().Last.WithInitialBounds(Lower, Upper));
        Assert.NotNull(Assembled().Last.WithInitialBounds(Lower, Lower));
        Assert.NotNull(Assembled().Last.WithInitialBounds(Upper, Upper));
    }

    /// <summary>Genes with one value fewer than N·D are an <see cref="ArgumentException"/> from <c>WithInitialPopulation</c>.</summary>
    [Fact]
    public void GenesOfFewerThanNTimesDValuesAreRefused()
    {
        var failure = Assert.Throws<ArgumentException>(
            () => Assembled().Last.WithInitialPopulation(Genes().AsMemory(0, PopulationSize * GenomeSize - 1)));

        Assert.Equal("genes", failure.ParamName);
        Assert.Contains("23 values", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Genes with one value more than N·D are refused.</summary>
    [Fact]
    public void GenesOfMoreThanNTimesDValuesAreRefused()
    {
        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialPopulation(Values([.. Genes(), 0.0])));

        Assert.Equal("genes", failure.ParamName);
    }

    /// <summary>A gene that is <c>NaN</c> or an infinity is refused, naming the individual and the gene.</summary>
    /// <param name="value">The value of gene 1 of individual 3.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void ANonFiniteGeneIsRefused(double value)
    {
        var genes = Genes();
        genes[3 * GenomeSize + 1] = value;

        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialPopulation(genes));

        Assert.Equal("genes", failure.ParamName);
        Assert.Contains("individual 3", failure.Message, StringComparison.Ordinal);
        Assert.Contains("gene 1", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A gene below the search box is refused, naming the individual and the gene.</summary>
    [Fact]
    public void AGeneBelowTheSearchBoxIsRefused()
    {
        var genes = Genes();
        genes[5 * GenomeSize + 2] = -0.001;

        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialPopulation(genes));

        Assert.Contains("individual 5", failure.Message, StringComparison.Ordinal);
        Assert.Contains("gene 2", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A gene above the search box is refused, naming the individual and the gene.</summary>
    [Fact]
    public void AGeneAboveTheSearchBoxIsRefused()
    {
        var genes = Genes();
        genes[7 * GenomeSize] = 2.001;

        var failure = Assert.Throws<ArgumentException>(() => Assembled().Last.WithInitialPopulation(genes));

        Assert.Contains("individual 7", failure.Message, StringComparison.Ordinal);
        Assert.Contains("gene 0", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The search box is closed: genes on its edges are accepted.</summary>
    [Fact]
    public void GenesOnTheSearchBoxsEdgeAreAccepted()
    {
        double[] onTheEdges = [.. Enumerable.Range(0, PopulationSize).SelectMany(i => i % 2 == 0 ? Lower : Upper)];

        Assert.NotNull(Assembled().Last.WithInitialPopulation(onTheEdges));
    }

    /// <summary>
    /// A later <c>WithBounds</c> on a retained stage that leaves the initial box outside the search box makes <c>Build</c>
    /// throw <see cref="InvalidOperationException"/>, naming the gene.
    /// </summary>
    [Fact]
    public void ABoxMadeUnfitByLaterBoundsIsRefusedByBuild()
    {
        var (bounds, _, last) = Assembled();
        _ = last.WithInitialBounds(Values(-1.0, -0.5, 1.0), Values(1.0, 0.5, 3.0));
        _ = bounds.WithBounds(Values(-2.0, -1.0, 2.0), Upper);

        var failure = Assert.Throws<InvalidOperationException>(last.Build);

        Assert.Contains("initial bounds", failure.Message, StringComparison.Ordinal);
        Assert.Contains("gene 2", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A later <c>WithBounds</c> that changes D makes <c>Build</c> refuse the initial box.</summary>
    [Fact]
    public void ABoxMadeUnfitByAnotherGenomeSizeIsRefusedByBuild()
    {
        var (bounds, _, last) = Assembled();
        _ = last.WithInitialBounds(Lower, Upper);
        _ = bounds.WithBounds(Values([.. Lower, -2.0]), Values([.. Upper, 2.0]));

        var failure = Assert.Throws<InvalidOperationException>(last.Build);

        Assert.Contains("initial lower bound has 3 values", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A later <c>WithPopulationSize</c> makes <c>Build</c> refuse the genes, naming what no longer fits.</summary>
    [Fact]
    public void GenesMadeUnfitByAnotherPopulationSizeAreRefusedByBuild()
    {
        var (_, size, last) = Assembled();
        _ = last.WithInitialPopulation(Genes());
        _ = size.WithPopulationSize(PopulationSize + 1);

        var failure = Assert.Throws<InvalidOperationException>(last.Build);

        Assert.Contains("initial population", failure.Message, StringComparison.Ordinal);
        Assert.Contains("24 values", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A later <c>WithBounds</c> that leaves a gene outside the search box makes <c>Build</c> refuse the genes, naming the individual and the gene.</summary>
    [Fact]
    public void GenesMadeUnfitByLaterBoundsAreRefusedByBuild()
    {
        var genes = Genes();
        genes[2 * GenomeSize + 1] = 0.75;
        var (bounds, _, last) = Assembled();
        _ = last.WithInitialPopulation(genes);
        _ = bounds.WithBounds(Lower, Values(2.0, 0.5, 4.0));

        var failure = Assert.Throws<InvalidOperationException>(last.Build);

        Assert.Contains("initial population", failure.Message, StringComparison.Ordinal);
        Assert.Contains("individual 2", failure.Message, StringComparison.Ordinal);
        Assert.Contains("gene 1", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Both calls on one builder, in either order, make <c>Build</c> throw <see cref="InvalidOperationException"/>.</summary>
    /// <param name="boundsFirst">Whether <c>WithInitialBounds</c> is called before <c>WithInitialPopulation</c>.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothCallsAreRefusedByBuild(bool boundsFirst)
    {
        var last = Assembled().Last;
        if (boundsFirst)
        {
            _ = last.WithInitialBounds(Lower, Upper);
            _ = last.WithInitialPopulation(Genes());
        }
        else
        {
            _ = last.WithInitialPopulation(Genes());
            _ = last.WithInitialBounds(Lower, Upper);
        }

        var failure = Assert.Throws<InvalidOperationException>(last.Build);

        Assert.Contains("WithInitialBounds", failure.Message, StringComparison.Ordinal);
        Assert.Contains("WithInitialPopulation", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The initial box is copied at the call, and a second call replaces the first.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void TheBoxIsCopiedAtTheCallAndASecondCallReplacesTheFirst()
    {
        double[] lower = [-2.0, -1.0, 0.0];
        double[] upper = [-1.5, -0.5, 0.5];
        var last = Assembled().Last;
        _ = last.WithInitialBounds(Values(1.5, 0.5, 3.5), Upper);
        _ = last.WithInitialBounds(lower, upper);
        lower[0] = 1.0;
        upper[0] = 2.0;

        using var optimizer = last.Build();
        var (genes, _) = optimizer.CurrentPopulation();

        Assert.Equal(PopulationSize * GenomeSize, genes.Length);
        double[] secondUpper = [-1.5, -0.5, 0.5];
        for (var k = 0; k < genes.Length; k++)
        {
            var gene = k % GenomeSize;
            Assert.True(
                genes[k] >= Lower[gene] && genes[k] <= secondUpper[gene],
                $"Gene {gene} of individual {k / GenomeSize} is {genes[k]:R}, outside the second call's box");
        }
    }

    /// <summary>The genes are copied at the call, and a second call replaces the first.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void TheGenesAreCopiedAtTheCallAndASecondCallReplacesTheFirst()
    {
        var first = Genes();
        var second = Genes();
        for (var k = 0; k < second.Length; k++)
        {
            second[k] = Upper[k % GenomeSize];
        }

        var last = Assembled().Last;
        _ = last.WithInitialPopulation(first);
        _ = last.WithInitialPopulation(second);
        second[0] = Lower[0];

        using var optimizer = last.Build();
        var (genes, fitness) = optimizer.CurrentPopulation();

        for (var k = 0; k < genes.Length; k++)
        {
            Assert.True(genes[k] == Upper[k % GenomeSize], $"Gene {k % GenomeSize} of individual {k / GenomeSize} is {genes[k]:R}, not the second call's");
        }

        Assert.Equal(PopulationSize, fitness.Length);
        Assert.Equal(PopulationSize, optimizer.EvaluationCount);
    }

    private static double[] Values(params double[] values) => values;

    /// <summary>N·D values strictly inside the search box, individual-major.</summary>
    private static double[] Genes() =>
        [.. Enumerable.Range(0, PopulationSize * GenomeSize).Select(k => Lower[k % GenomeSize] + (Upper[k % GenomeSize] - Lower[k % GenomeSize]) * (k + 1) / (PopulationSize * GenomeSize + 2))];

    private static (IGpuBoundsRequired<FiveSphere> Bounds, IGpuPopulationSizeRequired<FiveSphere> Size, IGpuDifferentialEvolutionBuilder<FiveSphere> Last) Assembled()
    {
        var bounds = GpuDifferentialEvolutionBuilder.ForFunction(default(FiveSphere));
        var size = bounds.WithBounds(Lower, Upper);
        var last = size
            .WithPopulationSize(PopulationSize)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(2)
            .OnDevice(GpuDevice.Cpu);
        return (bounds, size, last);
    }
}
