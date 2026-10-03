using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>
/// Check 1a of the GPU package's ACCEPTANCE.md, initial sampling. Seeded, N = 1000, D = 3, box
/// [−2, 5] in every gene: every gene is in [lower, upper); per gene a χ² test on 20 bins stays
/// under the 0.999 quantile (19 degrees of freedom, computed by <see cref="ChiSquared"/>); the
/// evaluation count after <c>Build</c> is N. The population is read by launching the package's
/// own initialization kernel through <see cref="KernelLauncher{TFunction}"/> on ILGPU's CPU
/// accelerator, into buffers laid out as the optimizer lays them out.
/// </summary>
/// <param name="output">Receives the χ² statistic of each gene.</param>
public class InitialSamplingTests(ITestOutputHelper output)
{
    private const int PopulationSize = 1000;
    private const int GenomeSize = 3;
    private const int Bins = 20;
    private const int Seed = 1;
    private const double LowerBound = -2.0;
    private const double UpperBound = 5.0;
    private static readonly double[] Lower = [LowerBound, LowerBound, LowerBound];
    private static readonly double[] Upper = [UpperBound, UpperBound, UpperBound];

    /// <summary>Every gene of the sampled population lies in [lower, upper).</summary>
    [Fact]
    public void EveryGeneIsInTheBox()
    {
        var genes = SampleInitialPopulation();

        Assert.Equal(PopulationSize * GenomeSize, genes.Length);
        for (var k = 0; k < genes.Length; k++)
        {
            Assert.True(
                genes[k] is >= LowerBound and < UpperBound,
                $"Gene {k % GenomeSize} of individual {k / GenomeSize} is {genes[k]:R}, outside [{LowerBound}, {UpperBound}).");
        }
    }

    /// <summary>
    /// For each gene, the counts of its N values in 20 equal bins of the box give a χ² statistic
    /// below the 0.999 quantile of χ² with 19 degrees of freedom.
    /// </summary>
    [Fact]
    public void EachGenePassesAChiSquaredTestOnTwentyBins()
    {
        var genes = SampleInitialPopulation();
        var threshold = ChiSquared.Quantile(0.999, Bins - 1);
        var expected = (double)PopulationSize / Bins;

        for (var j = 0; j < GenomeSize; j++)
        {
            var counts = new int[Bins];
            for (var i = 0; i < PopulationSize; i++)
            {
                var position = (genes[i * GenomeSize + j] - LowerBound) / (UpperBound - LowerBound);
                counts[Math.Clamp((int)Math.Floor(position * Bins), 0, Bins - 1)]++;
            }

            var statistic = ChiSquared.Statistic(counts, expected);
            output.WriteLine($"gene {j}: χ² = {statistic:F3}, 0.999 quantile (df = {Bins - 1}) = {threshold:F4}");
            Assert.True(
                statistic < threshold,
                $"Gene {j}: χ² = {statistic:R} is not under the 0.999 quantile {threshold:R}; counts {string.Join(", ", counts)}.");
        }
    }

    /// <summary><c>Build</c> samples and evaluates the initial population: N evaluations, before any generation.</summary>
    [Fact]
    public void BuildCostsNEvaluations()
    {
        using var optimizer = GpuDifferentialEvolutionBuilder.ForFunction(default(Sphere))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(PopulationSize)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(1)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(Seed)
            .Build();

        Assert.Equal(PopulationSize, optimizer.EvaluationCount);
    }

    private static double[] SampleInitialPopulation()
    {
        using var context = Context.Create(builder => builder.CPU().EnableAlgorithms());
        using var accelerator = context.CreateCPUAccelerator(0);
        using var current = accelerator.Allocate1D<double>(PopulationSize * GenomeSize);
        using var currentFitness = accelerator.Allocate1D<double>(PopulationSize);
        using var next = accelerator.Allocate1D<double>(PopulationSize * GenomeSize);
        using var nextFitness = accelerator.Allocate1D<double>(PopulationSize);
        using var trial = accelerator.Allocate1D<double>(PopulationSize * GenomeSize);
        using var lower = accelerator.Allocate1D(Lower);
        using var upper = accelerator.Allocate1D(Upper);
        var views = new PopulationViews(current.View, currentFitness.View, next.View, nextFitness.View, trial.View, lower.View, upper.View);
        var parameters = new StepParameters(Seed, 0, PopulationSize, GenomeSize, 0.5, DeStep.CrossoverThreshold(0.9));

        var launcher = new KernelLauncher<Sphere>(accelerator, default);
        launcher.Initialize(parameters, views);
        accelerator.Synchronize();

        return current.GetAsArray1D();
    }
}
