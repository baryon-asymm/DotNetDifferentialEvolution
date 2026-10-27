namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>The strategies of checks I1 to I7: "every strategy".</summary>
internal enum InitialStrategy
{
    /// <summary>DE/rand/1/bin with F 0.5 and CR 0.9.</summary>
    Classic = 0,

    /// <summary>jDE.</summary>
    Jde = 1,

    /// <summary>JADE.</summary>
    Jade = 2,

    /// <summary>SHADE.</summary>
    Shade = 3,

    /// <summary>L-SHADE, ended by a generation limit.</summary>
    LShade = 4,
}

/// <summary>The two forms of an objective of checks I1 to I7: "each with a monolithic and a pointwise objective of the same arithmetic".</summary>
internal enum ObjectiveForm
{
    /// <summary>One thread per individual: <see cref="FiveSphere"/>.</summary>
    Monolithic = 0,

    /// <summary>One thread per gene: <see cref="FiveSpherePointwise"/>.</summary>
    Pointwise = 1,
}

/// <summary>What one run of <see cref="InitialRuns"/> is told: the search box is [−10, 10] in every gene.</summary>
/// <param name="Strategy">The strategy.</param>
/// <param name="Form">The form of the objective.</param>
/// <param name="Device">The device: the CPU accelerator, or CUDA under <c>Gpu</c>.</param>
/// <param name="GenomeSize">D.</param>
/// <param name="PopulationSize">N; under L-SHADE the initial one.</param>
/// <param name="Generations">The generation limit.</param>
/// <param name="Seed">The seed.</param>
internal sealed record InitialSetup(
    InitialStrategy Strategy,
    ObjectiveForm Form,
    GpuDevice Device,
    int GenomeSize,
    int PopulationSize,
    int Generations,
    int Seed)
{
    /// <summary>Gets L-SHADE's evaluation budget, or <see langword="null"/> for N times the generations.</summary>
    public long? Budget { get; init; }

    /// <summary>Gets the lower bound of the initial box, or <see langword="null"/> for none.</summary>
    public double[]? InitialLower { get; init; }

    /// <summary>Gets the upper bound of the initial box, or <see langword="null"/> for none.</summary>
    public double[]? InitialUpper { get; init; }

    /// <summary>Gets the given genes, or <see langword="null"/> for none.</summary>
    public double[]? InitialGenes { get; init; }
}

/// <summary>
/// What one run left: generation 0 as <c>Build</c> left it, the population the run ended at, the result, and one hash and one
/// best individual for each generation's snapshot (an observer due every generation).
/// </summary>
/// <param name="FirstGenes">The genes of generation 0, read after <c>Build</c> through the package's internal access.</param>
/// <param name="FirstFitness">The fitness values of generation 0.</param>
/// <param name="LastGenes">The genes of the population the run ended at.</param>
/// <param name="LastFitness">The fitness values of that population.</param>
/// <param name="Run">The result and the snapshot hashes, as the other equivalence checks compare them.</param>
/// <param name="Bests">The best individual of each snapshot, in order.</param>
internal sealed record InitialOutcome(
    double[] FirstGenes,
    double[] FirstFitness,
    double[] LastGenes,
    double[] LastFitness,
    RunRecord Run,
    IReadOnlyList<KeptBest> Bests);

/// <summary>Keeps a hash and the best individual of every snapshot.</summary>
internal sealed class SnapshotLog : IGpuPopulationUpdatedHandler
{
    private readonly SnapshotHasher _hasher = new();
    private readonly List<KeptBest> _bests = [];

    /// <summary>Gets the hash of each snapshot, in order.</summary>
    public IReadOnlyList<string> Hashes => _hasher.Hashes;

    /// <summary>Gets the best individual of each snapshot, in order.</summary>
    public IReadOnlyList<KeptBest> Bests => _bests;

    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
        _hasher.Handle(snapshot);
        _bests.Add(KeptBest.Of(snapshot));
    }
}

/// <summary>Builds, reads generation 0 of, runs and reads the end of a run of any strategy and form, and compares the outcomes.</summary>
internal static class InitialRuns
{
    /// <summary>The lower edge of the search box in every gene.</summary>
    public const double SearchLower = -10.0;

    /// <summary>The upper edge of the search box in every gene.</summary>
    public const double SearchUpper = 10.0;

    /// <summary>Runs <paramref name="setup"/>: builds, reads generation 0, runs to the generation limit, reads the end.</summary>
    /// <param name="setup">The run.</param>
    /// <returns>What it left.</returns>
    public static Task<InitialOutcome> Execute(InitialSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return setup.Form == ObjectiveForm.Monolithic
            ? Execute(GpuDifferentialEvolutionBuilder.ForFunction(default(FiveSphere)), setup)
            : Execute(
                GpuDifferentialEvolutionBuilder.ForPointwiseFunction<FiveSpherePointwise, SquaredDeviation>(default, setup.GenomeSize),
                setup);
    }

    /// <summary>The search box's bounds, as the builder's first stage takes them.</summary>
    /// <param name="genomeSize">D.</param>
    /// <param name="value">The value of every gene.</param>
    /// <returns>D copies of <paramref name="value"/>.</returns>
    public static double[] Filled(int genomeSize, double value) => [.. Enumerable.Repeat(value, genomeSize)];

    /// <summary>Applies the strategy to the stage where the scheme is chosen.</summary>
    /// <typeparam name="TFunction">The objective.</typeparam>
    /// <param name="stage">The scheme stage.</param>
    /// <param name="strategy">The strategy.</param>
    /// <param name="budget">L-SHADE's evaluation budget.</param>
    /// <returns>The termination stage.</returns>
    public static IGpuTerminationConditionRequired<TFunction> Scheme<TFunction>(
        IGpuMutationStrategyRequired<TFunction> stage, InitialStrategy strategy, long budget)
        where TFunction : struct
    {
        ArgumentNullException.ThrowIfNull(stage);
        return strategy switch
        {
            InitialStrategy.Classic => stage.WithDefaultMutationStrategy(0.5, 0.9),
            InitialStrategy.Jde => stage.WithJde(),
            InitialStrategy.Jade => stage.WithJade(),
            InitialStrategy.Shade => stage.WithShade(),
            InitialStrategy.LShade => stage.WithLShade(budget),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Not a strategy."),
        };
    }

    /// <summary>Asserts that two arrays hold the same doubles bit for bit.</summary>
    /// <param name="expected">The reference.</param>
    /// <param name="actual">The array compared with it.</param>
    /// <param name="what">What is compared, for the messages.</param>
    public static void AssertBits(double[] expected, double[] actual, string what)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        Assert.True(expected.Length == actual.Length, $"{what}: {expected.Length} values and {actual.Length}");
        for (var k = 0; k < expected.Length; k++)
        {
            Assert.True(
                BitConverter.DoubleToInt64Bits(expected[k]) == BitConverter.DoubleToInt64Bits(actual[k]),
                $"{what}: value {k} is {expected[k]:R} and {actual[k]:R}");
        }
    }

    /// <summary>Asserts that two outcomes are the same bit for bit: generation 0, the end, the result and every snapshot.</summary>
    /// <param name="expected">The reference.</param>
    /// <param name="actual">The outcome compared with it.</param>
    /// <param name="what">What is compared, for the messages.</param>
    public static void AssertSame(InitialOutcome expected, InitialOutcome actual, string what)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        AssertBits(expected.FirstGenes, actual.FirstGenes, $"{what}, generation 0 genes");
        AssertBits(expected.FirstFitness, actual.FirstFitness, $"{what}, generation 0 fitness");
        AssertBits(expected.LastGenes, actual.LastGenes, $"{what}, final genes");
        AssertBits(expected.LastFitness, actual.LastFitness, $"{what}, final fitness");
        RunRecord.AssertSame(expected.Run, actual.Run, what);
    }

    private static async Task<InitialOutcome> Execute<TFunction>(IGpuBoundsRequired<TFunction> start, InitialSetup setup)
        where TFunction : struct
    {
        var searched = start
            .WithBounds(Filled(setup.GenomeSize, SearchLower), Filled(setup.GenomeSize, SearchUpper))
            .WithPopulationSize(setup.PopulationSize);
        var stage = Scheme(searched, setup.Strategy, setup.Budget ?? (long)setup.PopulationSize * setup.Generations)
            .WithGenerationLimit(setup.Generations)
            .OnDevice(setup.Device)
            .WithSeed(setup.Seed);
        if (setup.InitialLower is { } lower && setup.InitialUpper is { } upper)
        {
            stage = stage.WithInitialBounds(lower, upper);
        }

        if (setup.InitialGenes is { } genes)
        {
            stage = stage.WithInitialPopulation(genes);
        }

        var log = new SnapshotLog();
        using var optimizer = stage.WithPopulationUpdateHandler(log).Build();
        var (firstGenes, firstFitness) = optimizer.CurrentPopulation();
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        var (lastGenes, lastFitness) = optimizer.CurrentPopulation();
        var run = new RunRecord(
            result.Generations,
            result.EvaluationCount,
            BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue),
            [.. result.Genes.ToArray().Select(BitConverter.DoubleToInt64Bits)],
            log.Hashes);
        return new InitialOutcome(firstGenes, firstFitness, lastGenes, lastFitness, run, log.Bests);
    }
}
