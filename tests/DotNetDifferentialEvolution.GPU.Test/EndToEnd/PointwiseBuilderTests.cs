namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check P2 of the Kernels node's ACCEPTANCE.md, the builder of the pointwise objective: <c>ForPointwiseFunction</c> refuses a
/// point count below 1 with <see cref="ArgumentOutOfRangeException"/>, and <c>WithPopulationSize</c> refuses
/// <c>N·P &gt; int.MaxValue</c> with the exception it throws for <c>N·D</c>. The existing <c>ForFunction</c> tests, which
/// compile and pass unchanged against the relaxed stage interfaces, are the rest of the check.
/// </summary>
[Trait("Category", "Unit")]
public class PointwiseBuilderTests
{
    private static readonly double[] Lower = [0.0];
    private static readonly double[] Upper = [1.0];
    private static readonly double[] LowerOfTwo = [0.0, 0.0];
    private static readonly double[] UpperOfTwo = [1.0, 1.0];

    /// <summary>P2: a point count of 0 or −1 is an <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <param name="pointCount">The point count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APointCountBelowOneIsRejected(int pointCount)
    {
        var failure = Assert.Throws<ArgumentOutOfRangeException>(
            () => GpuDifferentialEvolutionBuilder.ForPointwiseFunction<PairPointwise, PairPoint>(default, pointCount));

        Assert.Equal("pointCount", failure.ParamName);
    }

    /// <summary>A point count of 1 is accepted.</summary>
    [Fact]
    public void APointCountOfOneIsAccepted() =>
        Assert.NotNull(GpuDifferentialEvolutionBuilder.ForPointwiseFunction<PairPointwise, PairPoint>(default, 1));

    /// <summary>
    /// P2: with D = 1, N = 2²⁰ and P = 2¹² make N·P = 2³², and <c>WithPopulationSize</c> throws the exception it throws for
    /// <c>N·D</c>. The boundary is exact: N·P = 2³¹ is refused and 2³¹ − 2¹⁰ is accepted.
    /// </summary>
    [Fact]
    public void APopulationWhosePointsExceedTheKernelsIndexRangeIsRejected()
    {
        var wide = GpuDifferentialEvolutionBuilder
            .ForPointwiseFunction<PairPointwise, PairPoint>(default, 1 << 12)
            .WithBounds(Lower, Upper);
        var narrow = GpuDifferentialEvolutionBuilder
            .ForPointwiseFunction<PairPointwise, PairPoint>(default, 1 << 10)
            .WithBounds(Lower, Upper);

        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => wide.WithPopulationSize(1 << 20));
        Assert.Equal("populationSize", failure.ParamName);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => narrow.WithPopulationSize(1 << 21));
        Assert.NotNull(narrow.WithPopulationSize((1 << 21) - 1));
    }

    /// <summary>The refusal of <c>N·D</c> is still the pointwise builder's too, as the single-kernel builder's.</summary>
    [Fact]
    public void APopulationBeyondTheGenesIndexRangeIsRejectedForAPointwiseObjectiveToo()
    {
        var stage = GpuDifferentialEvolutionBuilder
            .ForPointwiseFunction<PairPointwise, PairPoint>(default, 1)
            .WithBounds(LowerOfTwo, UpperOfTwo);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationSize(1 << 30));
        Assert.NotNull(stage.WithPopulationSize((1 << 30) - 1));
    }
}
