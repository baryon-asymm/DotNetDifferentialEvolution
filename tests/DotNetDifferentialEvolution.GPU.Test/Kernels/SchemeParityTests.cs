using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check S2: the same draws give bit-identical trials through the GPU's <see cref="Schemes.BuildTrial"/>
/// and the CPU package's fixed schemes. As check 1g: the CPU strategy runs on a <see cref="RecordingRandomProvider"/>,
/// its calls are replayed into a <see cref="ScriptedDraws"/> for the GPU, which must ask for exactly the same kinds,
/// ranges and number of draws. 100 random cases per scheme from a fixed seed: N in [6, 40], D in [1, 12], F in
/// [0.1, 2), CR with 0 and 1 included, a random best index, boxes narrow enough that mutants leave them; the repair
/// fires below and above (counted, and asserted).
/// </summary>
[Trait("Category", "Unit")]
public class SchemeParityTests
{
    private const int CaseCount = 100;
    private const int CaseSeed = 20261005;

    /// <summary>The five fixed schemes: the GPU's kind and the CPU strategy of the same name.</summary>
    /// <returns>The scheme, by name.</returns>
    public static TheoryData<string> FixedSchemes() =>
        [nameof(SchemeKind.RandOne), nameof(SchemeKind.Best), nameof(SchemeKind.CurrentToBest), nameof(SchemeKind.RandTwo), nameof(SchemeKind.BestTwo)];

    /// <summary>100 random cases of one scheme: GPU and CPU trials equal bit for bit, the same draws, the repair fired below and above.</summary>
    /// <param name="scheme">The scheme, by name.</param>
    [Theory]
    [MemberData(nameof(FixedSchemes))]
    public void TheGpuSchemeAndTheCpuStrategyBuildBitIdenticalTrials(string scheme)
    {
        var kind = Enum.Parse<SchemeKind>(scheme);
        var random = new SeededRandomProvider(CaseSeed + (int)kind);
        using var step = new HostStep();
        var repairedBelow = 0;
        var repairedAbove = 0;
        for (var testCase = 0; testCase < CaseCount; testCase++)
        {
            var populationSize = 6 + random.Next(35);
            var genomeSize = 1 + random.Next(12);
            var mutationForce = 0.1 + 1.9 * random.NextDouble();
            var crossoverProbability = (testCase % 10) switch
            {
                0 => 0.0,
                1 => 1.0,
                _ => random.NextDouble(),
            };
            var (lower, upper, population) = ParityCases.Box(random, populationSize, genomeSize);
            var individual = random.Next(populationSize);
            var best = random.Next(populationSize);

            var recorder = new RecordingRandomProvider(2000 + testCase);
            var cpuTrial = new double[genomeSize];
            var context = new MutationContext
            {
                IndividualIndex = individual,
                BestIndividualIndex = best,
                PopulationSize = populationSize,
                GenomeSize = genomeSize,
                MutationForce = mutationForce,
                CrossoverProbability = crossoverProbability,
                Population = population,
                TrialIndividual = cpuTrial,
                LowerBound = lower,
                UpperBound = upper,
                RandomProvider = recorder,
            };
            CpuStrategy(kind).Mutate(in context);

            var parameters = new StepParameters(0, 1, populationSize, genomeSize, mutationForce, 0UL, kind);
            var draws = new ScriptedDraws(recorder.Calls);
            var gpuTrial = step.BuildSchemeTrial(
                ref draws, individual, parameters, mutationForce, crossoverProbability, population, lower, upper, SchemeState.WithBest(best));

            Assert.True(recorder.Calls.Count == draws.Consumed, $"{scheme} case {testCase}: the CPU drew {recorder.Calls.Count} times, the GPU {draws.Consumed}");
            ParityCases.AssertSameBits(cpuTrial, gpuTrial, $"{scheme} case {testCase}");

            var unbounded = new ScriptedDraws(recorder.Calls);
            var unrepaired = step.BuildSchemeTrial(
                ref unbounded,
                individual,
                parameters,
                mutationForce,
                crossoverProbability,
                population,
                [.. Enumerable.Repeat(double.MinValue, genomeSize)],
                [.. Enumerable.Repeat(double.MaxValue, genomeSize)],
                SchemeState.WithBest(best));
            for (var j = 0; j < genomeSize; j++)
            {
                repairedBelow += unrepaired[j] < lower[j] ? 1 : 0;
                repairedAbove += unrepaired[j] > upper[j] ? 1 : 0;
            }
        }

        Assert.True(repairedBelow > 0 && repairedAbove > 0, $"{scheme}: repairs below {repairedBelow}, above {repairedAbove}");
    }

    private static IMutationStrategy CpuStrategy(SchemeKind kind) => kind switch
    {
        SchemeKind.RandOne => new RandMutationStrategy(),
        SchemeKind.Best => new BestMutationStrategy(),
        SchemeKind.CurrentToBest => new CurrentToBestMutationStrategy(),
        SchemeKind.RandTwo => new RandTwoMutationStrategy(),
        SchemeKind.BestTwo => new BestTwoMutationStrategy(),
        SchemeKind.CurrentToPBest => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a fixed scheme; check S3."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a scheme."),
    };
}
