using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check P1 of the Kernels node's ACCEPTANCE.md: a pointwise objective and its monolithic twin, the same arithmetic in the
/// same order (<see cref="PairArithmetic"/>, called by both), give the same run for each of the nine configurations of check
/// S13: the best genes, the fitness, the generations, the evaluations and every population snapshot (an observer every 25
/// generations, one SHA-256 per snapshot), bit for bit. D = 10 in [−5, 5], P = 12, N = 64 (L-SHADE N_init 64), seed 1,
/// 2·10⁴ evaluations; and SHADE with the stagnation rule (max streak 5, threshold 0) on a stepped objective of the same
/// shape, so that the run ends by the stop word; and the same run with the stop word read only at the end of 200
/// generations (the construction of S18), so that every kernel runs with the word set for some 190 generations and the
/// result is the stopping generation's only if they all did nothing. On the CPU accelerator in CI, on CUDA under <c>Gpu</c>.
/// </summary>
/// <param name="output">Receives the figures of each run.</param>
public class PointwiseEquivalenceTests(ITestOutputHelper output)
{
    private const long Budget = 20_000;
    private const int PopulationSize = 64;
    private const int ObserverPeriod = 25;
    private const int BeyondTheStopGenerations = 200;

    private static readonly double[] Lower = [.. Enumerable.Repeat(-5.0, PairArithmetic.GenomeSize)];
    private static readonly double[] Upper = [.. Enumerable.Repeat(5.0, PairArithmetic.GenomeSize)];

    /// <summary>P1 on the CPU accelerator: the pointwise run equals its monolithic twin's.</summary>
    /// <param name="configuration">The builder method.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(SymmetryRunTests.Configurations), MemberType = typeof(SymmetryRunTests))]
    public Task EachConfigurationEqualsItsMonolithicTwinOnTheCpuAccelerator(string configuration) =>
        AssertTwins(configuration, GpuDevice.Cpu);

    /// <summary>P1 on CUDA.</summary>
    /// <param name="configuration">The builder method.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(SymmetryRunTests.Configurations), MemberType = typeof(SymmetryRunTests))]
    public Task EachConfigurationEqualsItsMonolithicTwinOnCuda(string configuration) =>
        AssertTwins(configuration, GpuDevice.Cuda);

    /// <summary>P1 with the stagnation rule on the CPU accelerator: the run ends by the stop word at the twin's generation.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task TheStagnationRunEqualsItsMonolithicTwinOnTheCpuAccelerator() => AssertStagnationTwins(GpuDevice.Cpu);

    /// <summary>P1 with the stagnation rule on CUDA.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public Task TheStagnationRunEqualsItsMonolithicTwinOnCuda() => AssertStagnationTwins(GpuDevice.Cuda);

    /// <summary>P1 with the stagnation rule read only after 200 generations, on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Integration")]
    public Task TheKernelsDoNothingOnceTheStopWordIsSetOnTheCpuAccelerator() => AssertStopWordTwins(GpuDevice.Cpu);

    /// <summary>P1 with the stagnation rule read only after 200 generations, on CUDA.</summary>
    /// <returns>The case.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public Task TheKernelsDoNothingOnceTheStopWordIsSetOnCuda() => AssertStopWordTwins(GpuDevice.Cuda);

    private static IGpuTerminationConditionRequired<TFunction> Scheme<TFunction>(IGpuMutationStrategyRequired<TFunction> stage, string configuration)
        where TFunction : struct => configuration switch
        {
            nameof(IGpuMutationStrategyRequired<>.WithDefaultMutationStrategy) => stage.WithDefaultMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithBestMutationStrategy) => stage.WithBestMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithCurrentToBestMutationStrategy) => stage.WithCurrentToBestMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithRandTwoMutationStrategy) => stage.WithRandTwoMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithBestTwoMutationStrategy) => stage.WithBestTwoMutationStrategy(0.5, 0.9),
            nameof(IGpuMutationStrategyRequired<>.WithJde) => stage.WithJde(),
            nameof(IGpuMutationStrategyRequired<>.WithJade) => stage.WithJade(),
            nameof(IGpuMutationStrategyRequired<>.WithShade) => stage.WithShade(),
            nameof(IGpuMutationStrategyRequired<>.WithLShade) => stage.WithLShade(Budget),
            _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration, "Not a configuration."),
        };

    private static async Task<RunRecord> Run<TFunction>(IGpuDeviceRequired<TFunction> stage, GpuDevice device)
        where TFunction : struct
    {
        var hasher = new SnapshotHasher();
        using var optimizer = stage.OnDevice(device).WithSeed(1).WithPopulationUpdateHandler(hasher, ObserverPeriod).Build();
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        return new RunRecord(
            result.Generations,
            result.EvaluationCount,
            BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue),
            [.. result.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits)],
            hasher.Hashes);
    }

    /// <summary>
    /// SHADE with the stagnation rule and a generation limit beside it (which only the internal builder allows), the stop word
    /// read at the limit only: the rule fires early and the kernels run with the word set for the rest of the run.
    /// </summary>
    private static async Task<RunRecord> RunBeyondTheStop<TFunction>(IGpuBoundsRequired<TFunction> start, GpuDevice device)
        where TFunction : struct
    {
        var builder = (GpuBuilder<TFunction>)Start(start).WithShade().WithStagnationLimit(5, 0.0).OnDevice(device).WithSeed(1);
        _ = builder.WithGenerationLimit(BeyondTheStopGenerations);
        using var optimizer = builder.WithStopReadInterval(BeyondTheStopGenerations + 1).Build();
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        return new RunRecord(
            result.Generations,
            result.EvaluationCount,
            BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue),
            [.. result.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits)],
            []);
    }

    private static IGpuMutationStrategyRequired<TFunction> Start<TFunction>(IGpuBoundsRequired<TFunction> start)
        where TFunction : struct => start.WithBounds(Lower, Upper).WithPopulationSize(PopulationSize);

    private async Task AssertTwins(string configuration, GpuDevice device)
    {
        var twin = await Run(
            Scheme(Start(GpuDifferentialEvolutionBuilder.ForFunction(default(PairMonolithic))), configuration).WithEvaluationLimit(Budget),
            device).ConfigureAwait(true);
        var pointwise = await Run(
            Scheme(Start(GpuDifferentialEvolutionBuilder.ForPointwiseFunction<PairPointwise, PairPoint>(default, PairArithmetic.PointCount)), configuration)
                .WithEvaluationLimit(Budget),
            device).ConfigureAwait(true);

        output.WriteLine(
            $"{configuration} on {device}: {BitConverter.Int64BitsToDouble(pointwise.FitnessBits):R} after {pointwise.Generations} generations, " +
            $"{pointwise.SnapshotHashes.Count} snapshots; the monolithic twin {BitConverter.Int64BitsToDouble(twin.FitnessBits):R}");
        Assert.True(pointwise.SnapshotHashes.Count > 1, $"{configuration}: {pointwise.SnapshotHashes.Count} snapshots, too few to tell");
        Assert.True(double.IsFinite(BitConverter.Int64BitsToDouble(pointwise.FitnessBits)), $"{configuration}: the fitness is not finite");
        RunRecord.AssertSame(twin, pointwise, configuration);
    }

    private async Task AssertStagnationTwins(GpuDevice device)
    {
        var twin = await Run(
            Start(GpuDifferentialEvolutionBuilder.ForFunction(default(SteppedPairMonolithic))).WithShade().WithStagnationLimit(5, 0.0),
            device).ConfigureAwait(true);
        var pointwise = await Run(
            Start(GpuDifferentialEvolutionBuilder.ForPointwiseFunction<SteppedPairPointwise, PairPoint>(default, PairArithmetic.PointCount))
                .WithShade()
                .WithStagnationLimit(5, 0.0),
            device).ConfigureAwait(true);

        output.WriteLine(
            $"stagnation on {device}: stopped at generation {pointwise.Generations} after {pointwise.Evaluations} evaluations with " +
            $"{BitConverter.Int64BitsToDouble(pointwise.FitnessBits):R}; the monolithic twin at generation {twin.Generations}");
        Assert.True(pointwise.Generations > 5, $"the stagnation run stopped at generation {pointwise.Generations}, too early to tell");
        RunRecord.AssertSame(twin, pointwise, "stagnation");
    }

    private async Task AssertStopWordTwins(GpuDevice device)
    {
        var twin = await RunBeyondTheStop(GpuDifferentialEvolutionBuilder.ForFunction(default(SteppedPairMonolithic)), device).ConfigureAwait(true);
        var pointwise = await RunBeyondTheStop(
            GpuDifferentialEvolutionBuilder.ForPointwiseFunction<SteppedPairPointwise, PairPoint>(default, PairArithmetic.PointCount),
            device).ConfigureAwait(true);

        output.WriteLine(
            $"stop word on {device}: {BeyondTheStopGenerations} generations enqueued, the result of generation {pointwise.Generations} with " +
            $"{BitConverter.Int64BitsToDouble(pointwise.FitnessBits):R}; the monolithic twin generation {twin.Generations}");
        Assert.True(
            pointwise.Generations is > 5 and < BeyondTheStopGenerations / 2,
            $"the rule stopped at generation {pointwise.Generations}: too early or too late for {BeyondTheStopGenerations} generations to run beyond it");
        RunRecord.AssertSame(twin, pointwise, "stop word");
    }
}
