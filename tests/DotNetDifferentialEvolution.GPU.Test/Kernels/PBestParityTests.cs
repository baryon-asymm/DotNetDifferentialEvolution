using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check S3: the same draws give bit-identical trials through the GPU's current-to-pbest/1 and the CPU
/// package's <see cref="CurrentToPBestMutationStrategy"/>, as check S2 does for the fixed schemes. 100 random cases from a
/// fixed seed, with a random ranking and an archive of random size in [0, N]: half with p fixed (JADE, L-SHADE), half
/// with p drawn from [min(2/N, p), p] (SHADE). Asserted besides: r2 fell into the archive in some case, and the p-best
/// pool was clamped up to 2 in some case.
/// </summary>
[Trait("Category", "Unit")]
public class PBestParityTests
{
    private const int CaseCount = 100;
    private const int CaseSeed = 20261006;

    /// <summary>100 random cases: GPU and CPU trials equal bit for bit, the same draws; the archive and the clamp were both exercised.</summary>
    [Fact]
    public void TheGpuSchemeAndTheCpuStrategyBuildBitIdenticalTrials()
    {
        var random = new SeededRandomProvider(CaseSeed);
        using var step = new HostStep();
        var fromTheArchive = 0;
        var clampedToTwo = 0;
        for (var testCase = 0; testCase < CaseCount; testCase++)
        {
            var populationSize = 4 + random.Next(37);
            var genomeSize = 1 + random.Next(12);
            var mutationForce = 0.05 + 0.95 * random.NextDouble();
            var crossoverProbability = random.NextDouble();
            var (lower, upper, population) = ParityCases.Box(random, populationSize, genomeSize);
            var individual = random.Next(populationSize);
            var ranking = Enumerable.Range(0, populationSize).OrderBy(_ => random.NextDouble()).ToArray();
            var archiveSize = random.Next(populationSize + 1);
            var archive = new double[populationSize * genomeSize];
            for (var k = 0; k < archive.Length; k++)
            {
                var j = k % genomeSize;
                archive[k] = lower[j] + random.NextDouble() * (upper[j] - lower[j]);
            }

            var fixedRate = testCase % 2 == 0;
            var pBestRate = testCase % 10 == 0 ? 0.01 : 0.01 + 0.99 * random.NextDouble();
            var pBestRateMin = fixedRate ? pBestRate : Math.Min(2.0 / populationSize, pBestRate);
            var strategy = fixedRate
                ? new CurrentToPBestMutationStrategy(pBestRate)
                : new CurrentToPBestMutationStrategy(pBestRateMin, pBestRate);

            var recorder = new RecordingRandomProvider(3000 + testCase);
            var cpuTrial = new double[genomeSize];
            var context = new MutationContext
            {
                IndividualIndex = individual,
                PopulationSize = populationSize,
                GenomeSize = genomeSize,
                MutationForce = mutationForce,
                CrossoverProbability = crossoverProbability,
                Population = population,
                TrialIndividual = cpuTrial,
                LowerBound = lower,
                UpperBound = upper,
                RandomProvider = recorder,
                Archive = archive,
                ArchiveSize = archiveSize,
                FitnessSortedIndices = ranking,
            };
            strategy.Mutate(in context);

            var parameters = new StepParameters(
                0, 1, populationSize, genomeSize, mutationForce, 0UL, SchemeKind.CurrentToPBest, PBestRateMin: pBestRateMin, PBestRateMax: pBestRate);
            var draws = new ScriptedDraws(recorder.Calls);
            var gpuTrial = step.BuildSchemeTrial(
                ref draws,
                individual,
                parameters,
                mutationForce,
                crossoverProbability,
                population,
                lower,
                upper,
                new SchemeState(0, ranking, archive, archiveSize));

            Assert.True(recorder.Calls.Count == draws.Consumed, $"case {testCase}: the CPU drew {recorder.Calls.Count} times, the GPU {draws.Consumed}");
            ParityCases.AssertSameBits(cpuTrial, gpuTrial, $"case {testCase}");

            var unionDraws = recorder.Calls.Where(call => call.Kind == DrawKind.Index && call.Range == populationSize + archiveSize).ToList();
            fromTheArchive += archiveSize > 0 && unionDraws.Count > 0 && (int)unionDraws[^1].Value >= populationSize ? 1 : 0;
            var poolDraw = recorder.Calls.First(call => call.Kind == DrawKind.Index);
            clampedToTwo += fixedRate && Math.Round(pBestRate * populationSize, MidpointRounding.AwayFromZero) < 2 && poolDraw.Range == 2 ? 1 : 0;
        }

        Assert.True(fromTheArchive > 0, "r2 never fell into the archive");
        Assert.True(clampedToTwo > 0, "the p-best pool was never clamped up to 2");
    }
}
