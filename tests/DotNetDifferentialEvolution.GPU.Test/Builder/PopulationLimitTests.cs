using System.Globalization;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Check A9 of the GPU package's Kernels ACCEPTANCE.md, sizes a kernel can index. <c>WithPopulationSize</c> refuses N above
/// <c>int.MaxValue − 1 023</c> and, for a pointwise objective, N·P above it, with <see cref="ArgumentOutOfRangeException"/>;
/// <c>Build</c> checks N·D and N·P again with <see cref="InvalidOperationException"/>, so that a second <c>WithBounds</c> on
/// a retained stage cannot pass them; JADE, SHADE and L-SHADE refuse N above 2³⁰ at <c>Build</c>, whose ranking rounds N up
/// to a power of two. P2's cases (<c>PointwiseBuilderTests</c>) hold N·D and N·P at their edges and stay as they are.
/// The accepted edges are populations too large to allocate, so they are checked by
/// <c>GpuBuilder.ValidateConfiguration</c>, which runs what <c>Build</c> runs before it opens a device.
/// </summary>
public class PopulationLimitTests
{
    /// <summary><c>int.MaxValue − 1 023</c>, the largest N, and the largest N·P, a kernel can index.</summary>
    private const int Limit = int.MaxValue - 1023;

    /// <summary>2³⁰, the largest N the ranking of JADE, SHADE and L-SHADE can index.</summary>
    private const int RankingLimit = 1 << 30;

    private static readonly double[] OneGeneLower = [-1.0];
    private static readonly double[] OneGeneUpper = [1.0];
    private static readonly double[] TwoGenesLower = [-1.0, -1.0];
    private static readonly double[] TwoGenesUpper = [1.0, 1.0];

    /// <summary>The ranking schemes, by the method that selects each.</summary>
    /// <returns>The method's name.</returns>
    public static TheoryData<string> RankingSchemes() =>
    [
        nameof(IGpuMutationStrategyRequired<>.WithJade),
        nameof(IGpuMutationStrategyRequired<>.WithShade),
        nameof(IGpuMutationStrategyRequired<>.WithLShade),
    ];

    /// <summary>N one above the limit is an <see cref="ArgumentOutOfRangeException"/> naming N and the limit; N at the limit is accepted.</summary>
    [Fact]
    public void APopulationAboveTheLastGroupsIndexLimitIsRejected()
    {
        var stage = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere)).WithBounds(OneGeneLower, OneGeneUpper);

        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationSize(Limit + 1));
        Assert.Equal("populationSize", failure.ParamName);
        Assert.Contains($"N = {Limit + 1} ", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{Limit}", failure.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationSize(int.MaxValue));
        Assert.NotNull(stage.WithPopulationSize(Limit));
    }

    /// <summary>N = 4 and P = 536 870 911 give N·P = 2³¹ − 4, above the limit: refused, naming the product and the limit.</summary>
    [Fact]
    public void FourIndividualsOfFiveHundredThirtySixMillionPointsAreRejected()
    {
        var stage = Pointwise(536_870_911);

        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => stage.WithPopulationSize(4));
        Assert.Equal("populationSize", failure.ParamName);
        Assert.Contains($"N·P = {4L * 536_870_911}", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{Limit}", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>N·P one above the limit is refused; N·P equal to the limit, 2³¹ − 2¹⁰ (P2's edge), is accepted.</summary>
    [Fact]
    public void APointwisePopulationIsRejectedOneAboveTheLimitAndAcceptedAtIt()
    {
        var above = Pointwise(429_496_525);
        var at = Pointwise(2_097_151);

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => above.WithPopulationSize(5));
        Assert.NotNull(at.WithPopulationSize(1024));
        Assert.Equal(Limit, 1024L * 2_097_151);
        Assert.Equal(Limit + 1L, 5L * 429_496_525);
    }

    /// <summary>
    /// A stage retained across a second <c>WithBounds</c> reaches <c>Build</c> with N·D beyond <see cref="int.MaxValue"/>:
    /// <c>Build</c> refuses it with an <see cref="InvalidOperationException"/> naming the product and the limit, before it
    /// opens a device. N = 2³⁰ − 1 and a bound of 2²⁰ genes make 2⁵⁰ − 2²⁰ genes, which no allocation could satisfy.
    /// </summary>
    [Fact]
    public void ARetainedStageWithLongerBoundsIsRefusedByBuild()
    {
        var bounds = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));
        var device = bounds.WithBounds(TwoGenesLower, TwoGenesUpper)
            .WithPopulationSize((1 << 30) - 1)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(1)
            .OnDevice(GpuDevice.Cpu);
        _ = bounds.WithBounds(new double[1 << 20], Enumerable.Repeat(1.0, 1 << 20).ToArray());

        var failure = Assert.Throws<InvalidOperationException>(device.Build);

        var genes = (((1L << 30) - 1) * (1L << 20)).ToString(CultureInfo.InvariantCulture);
        Assert.Contains($"N·D = {genes}", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{int.MaxValue}", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The edge of the same bypass: N = 2³⁰ and a retained stage whose bound grows from one gene to two give N·D = 2³¹, one
    /// above <see cref="int.MaxValue"/>, which the configuration check refuses; with N = 2³⁰ − 1 the same growth gives
    /// 2³¹ − 2 and passes.
    /// </summary>
    [Fact]
    public void ARetainedStageIsRefusedOneAboveTheGenesIndexRangeAndAcceptedBelowIt()
    {
        var refused = Retained(1 << 30);
        var accepted = Retained((1 << 30) - 1);

        var failure = Assert.Throws<InvalidOperationException>(() => Validate(refused));
        Assert.Contains($"N·D = {1L << 31}", failure.Message, StringComparison.Ordinal);
        Validate(accepted);
    }

    /// <summary>A retained stage whose second bound is longer but within the range builds and runs on the CPU accelerator.</summary>
    [Fact]
    public void ARetainedStageWithinTheRangeBuilds()
    {
        var bounds = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));
        var device = bounds.WithBounds(OneGeneLower, OneGeneUpper)
            .WithPopulationSize(8)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(1)
            .OnDevice(GpuDevice.Cpu);
        _ = bounds.WithBounds(TwoGenesLower, TwoGenesUpper);

        using var optimizer = device.Build();

        Assert.Equal(GpuDevice.Cpu, optimizer.Device.Kind);
    }

    /// <summary>
    /// JADE, SHADE and L-SHADE refuse N = 2³⁰ + 1 at <c>Build</c> with an <see cref="InvalidOperationException"/> naming the
    /// scheme and the ranking's limit, 2³⁰, before any device is opened.
    /// </summary>
    /// <param name="method">The scheme stage method.</param>
    [Theory]
    [MemberData(nameof(RankingSchemes))]
    public void ARankingSchemeRefusesAPopulationAboveTwoToTheThirtyAtBuild(string method)
    {
        var device = Ranked(method, RankingLimit + 1).WithGenerationLimit(1).OnDevice(GpuDevice.Cpu);

        var failure = Assert.Throws<InvalidOperationException>(device.Build);

        Assert.Contains(method, failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{RankingLimit}", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("archive", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The ranking schemes accept N = 2³⁰, the limit itself, in the configuration check (the run would allocate gigabytes).</summary>
    /// <param name="method">The scheme stage method.</param>
    [Theory]
    [MemberData(nameof(RankingSchemes))]
    public void ARankingSchemeAcceptsAPopulationOfTwoToTheThirty(string method) =>
        Validate(Ranked(method, RankingLimit).WithGenerationLimit(1).OnDevice(GpuDevice.Cpu));

    /// <summary>The schemes that do not rank are not held to 2³⁰: rand/1 and jDE pass the configuration check at N = 2³⁰ + 1.</summary>
    [Fact]
    public void TheSchemesThatDoNotRankAcceptAPopulationAboveTwoToTheThirty()
    {
        var randOne = Stage(RankingLimit + 1).WithDefaultMutationStrategy(0.5, 0.9).WithGenerationLimit(1).OnDevice(GpuDevice.Cpu);
        var jde = Stage(RankingLimit + 1).WithJde().WithGenerationLimit(1).OnDevice(GpuDevice.Cpu);

        Validate(randOne);
        Validate(jde);
    }

    private static void Validate(IGpuDifferentialEvolutionBuilder<Sphere> builder) => ((GpuBuilder<Sphere>)builder).ValidateConfiguration();

    private static IGpuPopulationSizeRequired<PointProbe<double>> Pointwise(int pointCount) =>
        GpuDifferentialEvolutionBuilder.ForPointwiseFunction<PointProbe<double>, double>(default, pointCount)
            .WithBounds(OneGeneLower, OneGeneUpper);

    private static IGpuMutationStrategyRequired<Sphere> Stage(int populationSize) =>
        GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(OneGeneLower, OneGeneUpper)
            .WithPopulationSize(populationSize);

    /// <summary>A stage of N individuals and one gene, then retained: its bound grows to two genes afterwards.</summary>
    private static IGpuDifferentialEvolutionBuilder<Sphere> Retained(int populationSize)
    {
        var bounds = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere));
        var device = bounds.WithBounds(OneGeneLower, OneGeneUpper)
            .WithPopulationSize(populationSize)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(1)
            .OnDevice(GpuDevice.Cpu);
        _ = bounds.WithBounds(TwoGenesLower, TwoGenesUpper);
        return device;
    }

    /// <summary>A stage of one gene for the ranking scheme the method names, with an archive of N genes, within the range.</summary>
    private static IGpuTerminationConditionRequired<Sphere> Ranked(string method, int populationSize)
    {
        var stage = Stage(populationSize);
        return method switch
        {
            nameof(IGpuMutationStrategyRequired<>.WithJade) => stage.WithJade(),
            nameof(IGpuMutationStrategyRequired<>.WithShade) => stage.WithShade(),
            nameof(IGpuMutationStrategyRequired<>.WithLShade) => stage.WithLShade(1000, archiveSizeRate: 1.0),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Not a ranking scheme."),
        };
    }
}
