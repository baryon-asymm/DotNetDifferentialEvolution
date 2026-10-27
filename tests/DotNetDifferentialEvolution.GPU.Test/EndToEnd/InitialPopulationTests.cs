using System.Globalization;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Checks I2 to I5 and I7 of the Kernels node's ACCEPTANCE.md, the initial population (1.2.0). "Every strategy" is Classic,
/// jDE, JADE, SHADE and L-SHADE (generation limit), each with a monolithic and a pointwise objective of the same arithmetic
/// (<see cref="FiveSphere"/>); the search box is [−10, 10] in every gene. Generation 0 is read after <c>Build</c> through the
/// package's internal access. On the CPU accelerator in CI; I7 repeats I2, I3's zero-width box and seeded equality, and I5's
/// reproduction on CUDA under <c>Gpu</c>. I1 is the existing tests, unchanged, and the orchestrator's probe; I6 is
/// <see cref="InitialPopulationRefusalTests"/>. The cases name the strategy and the form by the names of their enums.
/// </summary>
/// <param name="output">Receives the figures of each run.</param>
public class InitialPopulationTests(ITestOutputHelper output)
{
    private const int GenomeSize = 5;
    private const int PopulationSize = 64;
    private const int Generations = 20;

    /// <summary>Every strategy with each form of the objective.</summary>
    /// <returns>The ten cases.</returns>
    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var strategy in Enum.GetNames<InitialStrategy>())
        {
            foreach (var form in Enum.GetNames<ObjectiveForm>())
            {
                data.Add(strategy, form);
            }
        }

        return data;
    }

    /// <summary>Both forms of the objective.</summary>
    /// <returns>The two cases.</returns>
    public static TheoryData<string> Forms() => [.. Enum.GetNames<ObjectiveForm>()];

    /// <summary>I2 on the CPU accelerator: an initial box equal to the search box is no box.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Cases))]
    public Task AnInitialBoxEqualToTheSearchBoxIsNoBox(string strategy, string form) =>
        AssertBoxEqualToSearchBox(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cpu);

    /// <summary>I7: I2 on CUDA.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(Cases))]
    public Task AnInitialBoxEqualToTheSearchBoxIsNoBoxOnCuda(string strategy, string form) =>
        AssertBoxEqualToSearchBox(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cuda);

    /// <summary>
    /// I3: N 256, D 5, an initial box narrower than the search box on genes 0, 2 and 4 and equal on 1 and 3: every gene of
    /// generation 0 inside its initial interval.
    /// </summary>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Forms))]
    public async Task GenerationZeroLiesInTheInitialBox(string form)
    {
        double[] lower = [-3.0, InitialRuns.SearchLower, 2.0, InitialRuns.SearchLower, -7.5];
        double[] upper = [-2.0, InitialRuns.SearchUpper, 2.5, InitialRuns.SearchUpper, -7.0];
        var setup = new InitialSetup(InitialStrategy.Classic, Enum.Parse<ObjectiveForm>(form), GpuDevice.Cpu, GenomeSize, 256, 1, 1)
        {
            InitialLower = lower,
            InitialUpper = upper,
        };

        var outcome = await InitialRuns.Execute(setup).ConfigureAwait(true);

        Assert.Equal(256 * GenomeSize, outcome.FirstGenes.Length);
        for (var k = 0; k < outcome.FirstGenes.Length; k++)
        {
            var gene = k % GenomeSize;
            Assert.True(
                outcome.FirstGenes[k] >= lower[gene] && outcome.FirstGenes[k] <= upper[gene],
                $"Gene {gene} of individual {k / GenomeSize} is {outcome.FirstGenes[k]:R}, outside [{lower[gene]}, {upper[gene]}].");
        }

        output.WriteLine($"{form}: {outcome.FirstGenes.Length} genes of generation 0 inside the initial box");
    }

    /// <summary>I3: a zero-width box at a point c: every individual equals c bit for bit and every fitness is the same value.</summary>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Forms))]
    public Task AZeroWidthBoxGivesEveryIndividualItsPoint(string form) =>
        AssertZeroWidthBox(Enum.Parse<ObjectiveForm>(form), GpuDevice.Cpu);

    /// <summary>I7: I3's zero-width box on CUDA.</summary>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(Forms))]
    public Task AZeroWidthBoxGivesEveryIndividualItsPointOnCuda(string form) =>
        AssertZeroWidthBox(Enum.Parse<ObjectiveForm>(form), GpuDevice.Cuda);

    /// <summary>I3: two seeded builds with one initial box are equal bit for bit at generation 0 and at 20.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Cases))]
    public Task TwoSeededRunsWithOneInitialBoxAreTheSameRun(string strategy, string form) =>
        AssertSeededEquality(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cpu);

    /// <summary>I7: I3's seeded equality on CUDA.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(Cases))]
    public Task TwoSeededRunsWithOneInitialBoxAreTheSameRunOnCuda(string strategy, string form) =>
        AssertSeededEquality(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cuda);

    /// <summary>
    /// I4: Sphere with its optimum at 5 on every gene, search box [−10, 10]⁴, initial box [−10, −9]⁴, N 64, 300 generations,
    /// seed 1: the best individual has every gene above −9 and a fitness below 1e-6.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Cases))]
    public async Task TheInitialBoxLimitsTheStartOnly(string strategy, string form)
    {
        const int genomeSize = 4;
        var setup = new InitialSetup(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cpu, genomeSize, 64, 300, 1)
        {
            InitialLower = InitialRuns.Filled(genomeSize, -10.0),
            InitialUpper = InitialRuns.Filled(genomeSize, -9.0),
        };

        var outcome = await InitialRuns.Execute(setup).ConfigureAwait(true);

        var best = outcome.Bests[^1];
        var genes = string.Join(", ", best.Genes.Select(gene => gene.ToString("R", CultureInfo.InvariantCulture)));
        output.WriteLine($"{strategy}, {form}: best genes {genes}, fitness {best.Fitness:R}");
        Assert.True(
            outcome.Run.Generations == 300 && BitConverter.Int64BitsToDouble(outcome.Run.FitnessBits) == best.Fitness,
            $"{strategy}, {form}: the last snapshot is not the result");
        Assert.All(best.Genes, gene => Assert.True(gene > -9.0, $"{strategy}, {form}: a gene of the best individual is {gene:R}, not above -9"));
        Assert.True(best.Fitness < 1e-6, $"{strategy}, {form}: the best fitness is {best.Fitness:R}, not below 1e-6");
    }

    /// <summary>
    /// I5 on the CPU accelerator: a seeded run S, its generation-0 population passed to <c>WithInitialPopulation</c> of a run
    /// with S's configuration and seed, reproduces S bit for bit through generation 20 (genes, fitness, counts).
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Cases))]
    public Task GivenGenesAreGenerationZero(string strategy, string form) =>
        AssertGivenGenesReproduce(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cpu);

    /// <summary>I7: I5's reproduction of S on CUDA.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [MemberData(nameof(Cases))]
    public Task GivenGenesAreGenerationZeroOnCuda(string strategy, string form) =>
        AssertGivenGenesReproduce(Enum.Parse<InitialStrategy>(strategy), Enum.Parse<ObjectiveForm>(form), GpuDevice.Cuda);

    /// <summary>
    /// I5 on the CPU accelerator: the final population of a seeded 30-generation run R, passed to a run of the same
    /// configuration and another seed: generation 0 equals R's final genes and fitness bit for bit, and no snapshot of the next
    /// 10 generations, nor the result, has a best worse than R's. Under L-SHADE R's final population is smaller than the
    /// initial one, so the second run's population size is R's final size, with the same budget.
    /// </summary>
    /// <param name="strategy">The strategy.</param>
    /// <param name="form">The form of the objective.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(Cases))]
    public async Task AFinishedRunsPopulationContinuesWithoutGettingWorse(string strategy, string form)
    {
        const long budget = 64 * 100;
        var kind = Enum.Parse<InitialStrategy>(strategy);
        var shape = Enum.Parse<ObjectiveForm>(form);
        var finished = await InitialRuns.Execute(
            new InitialSetup(kind, shape, GpuDevice.Cpu, GenomeSize, PopulationSize, 30, 1) { Budget = budget }).ConfigureAwait(true);
        var finishedBest = BitConverter.Int64BitsToDouble(finished.Run.FitnessBits);

        var continued = await InitialRuns.Execute(
            new InitialSetup(kind, shape, GpuDevice.Cpu, GenomeSize, finished.LastFitness.Length, 10, 2)
            {
                Budget = budget,
                InitialGenes = finished.LastGenes,
            }).ConfigureAwait(true);

        output.WriteLine(
            $"{strategy}, {form}: R ends at {finishedBest:R} with {finished.LastFitness.Length} individuals; the continuation ends at {continued.Bests[^1].Fitness:R}");
        InitialRuns.AssertBits(finished.LastGenes, continued.FirstGenes, $"{strategy}, {form}: generation 0 of the continuation, genes");
        InitialRuns.AssertBits(finished.LastFitness, continued.FirstFitness, $"{strategy}, {form}: generation 0 of the continuation, fitness");
        Assert.Equal(10, continued.Bests.Count);
        foreach (var snapshot in continued.Bests)
        {
            Assert.True(
                snapshot.Fitness <= finishedBest,
                $"{strategy}, {form}: the best of generation {snapshot.Generation} is {snapshot.Fitness:R}, worse than R's {finishedBest:R}");
        }

        Assert.True(
            BitConverter.Int64BitsToDouble(continued.Run.FitnessBits) <= finishedBest,
            $"{strategy}, {form}: the result of the continuation is worse than R's");
    }

    private static InitialSetup Narrow(InitialStrategy strategy, ObjectiveForm form, GpuDevice device) =>
        new(strategy, form, device, GenomeSize, PopulationSize, Generations, 1)
        {
            InitialLower = [-6.0, -10.0, 1.0, -2.5, 3.0],
            InitialUpper = [-5.0, 10.0, 1.5, -2.0, 9.5],
        };

    private async Task AssertBoxEqualToSearchBox(InitialStrategy strategy, ObjectiveForm form, GpuDevice device)
    {
        var plain = await InitialRuns.Execute(new InitialSetup(strategy, form, device, GenomeSize, PopulationSize, Generations, 1)).ConfigureAwait(true);
        var boxed = await InitialRuns.Execute(
            new InitialSetup(strategy, form, device, GenomeSize, PopulationSize, Generations, 1)
            {
                InitialLower = InitialRuns.Filled(GenomeSize, InitialRuns.SearchLower),
                InitialUpper = InitialRuns.Filled(GenomeSize, InitialRuns.SearchUpper),
            }).ConfigureAwait(true);

        output.WriteLine($"{strategy}, {form} on {device}: best {BitConverter.Int64BitsToDouble(plain.Run.FitnessBits):R} after {plain.Run.Generations} generations");
        InitialRuns.AssertSame(plain, boxed, $"{strategy}, {form} on {device}");
    }

    private async Task AssertZeroWidthBox(ObjectiveForm form, GpuDevice device)
    {
        double[] point = [1.5, -2.25, 3.0, 0.0, 7.125];
        var setup = new InitialSetup(InitialStrategy.Classic, form, device, GenomeSize, 256, 1, 1)
        {
            InitialLower = point,
            InitialUpper = point,
        };

        var outcome = await InitialRuns.Execute(setup).ConfigureAwait(true);

        for (var k = 0; k < outcome.FirstGenes.Length; k++)
        {
            Assert.True(
                BitConverter.DoubleToInt64Bits(outcome.FirstGenes[k]) == BitConverter.DoubleToInt64Bits(point[k % GenomeSize]),
                $"Gene {k % GenomeSize} of individual {k / GenomeSize} is {outcome.FirstGenes[k]:R}, not {point[k % GenomeSize]:R}");
        }

        Assert.All(
            outcome.FirstFitness,
            fitness => Assert.True(
                BitConverter.DoubleToInt64Bits(fitness) == BitConverter.DoubleToInt64Bits(outcome.FirstFitness[0]),
                $"A fitness is {fitness:R}, not {outcome.FirstFitness[0]:R}"));
        output.WriteLine($"{form} on {device}: 256 individuals equal the point, fitness {outcome.FirstFitness[0]:R}");
    }

    private async Task AssertSeededEquality(InitialStrategy strategy, ObjectiveForm form, GpuDevice device)
    {
        var setup = Narrow(strategy, form, device);
        var first = await InitialRuns.Execute(setup).ConfigureAwait(true);
        var second = await InitialRuns.Execute(setup).ConfigureAwait(true);

        for (var k = 0; k < first.FirstGenes.Length; k++)
        {
            var gene = k % GenomeSize;
            Assert.True(
                first.FirstGenes[k] >= setup.InitialLower![gene] && first.FirstGenes[k] <= setup.InitialUpper![gene],
                $"{strategy}, {form}: gene {gene} of individual {k / GenomeSize} is {first.FirstGenes[k]:R}, outside the initial interval");
        }

        output.WriteLine($"{strategy}, {form} on {device}: best {BitConverter.Int64BitsToDouble(first.Run.FitnessBits):R} after {first.Run.Generations} generations");
        InitialRuns.AssertSame(first, second, $"{strategy}, {form} on {device}");
    }

    private async Task AssertGivenGenesReproduce(InitialStrategy strategy, ObjectiveForm form, GpuDevice device)
    {
        var sampled = new InitialSetup(strategy, form, device, GenomeSize, PopulationSize, Generations, 1);
        var original = await InitialRuns.Execute(sampled).ConfigureAwait(true);
        var given = await InitialRuns.Execute(sampled with { InitialGenes = original.FirstGenes }).ConfigureAwait(true);

        output.WriteLine(
            $"{strategy}, {form} on {device}: S ends at {BitConverter.Int64BitsToDouble(original.Run.FitnessBits):R} after {original.Run.Generations} generations, {original.Run.Evaluations} evaluations");
        Assert.Equal(Generations, original.Run.SnapshotHashes.Count);
        InitialRuns.AssertSame(original, given, $"{strategy}, {form} on {device}");
    }
}
