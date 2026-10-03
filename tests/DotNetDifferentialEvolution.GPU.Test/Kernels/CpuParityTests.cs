using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 1g: the same draws give bit-identical trial vectors through the GPU
/// <see cref="DeStep.BuildTrial"/> and through the CPU package's <c>DE/rand/1/bin</c>
/// (<see cref="MutationStrategy.Mutate"/>, which ends in <c>CrossoverHelper.BinomialCrossoverAndRepair</c>).
/// The CPU step runs on a <see cref="RecordingRandomProvider"/>; its calls are replayed, in order, into
/// a <see cref="ScriptedDraws"/> for the GPU step, which must ask for exactly the same kinds, ranges
/// and number of draws. 100 random cases from a fixed seed: N in [4, 40], D in [1, 12], F in
/// [0.1, 2), CR with 0 and 1 included, and boxes narrow enough that mutants leave them, so the repair
/// is exercised on both sides (counted, and asserted).
/// </summary>
[Trait("Category", "Unit")]
public class CpuParityTests
{
    private const int CaseCount = 100;
    private const int CaseSeed = 20261003;

    /// <summary>100 random cases: GPU and CPU trials equal bit for bit, and the repair fired below and above.</summary>
    [Fact]
    public void TheGpuStepAndTheCpuStrategyBuildBitIdenticalTrials()
    {
        var random = new SeededRandomProvider(CaseSeed);
        using var step = new HostStep();
        var repairedBelow = 0;
        var repairedAbove = 0;
        for (var testCase = 0; testCase < CaseCount; testCase++)
        {
            var populationSize = 4 + random.Next(37);
            var genomeSize = 1 + random.Next(12);
            var mutationForce = 0.1 + 1.9 * random.NextDouble();
            var crossoverProbability = (testCase % 10) switch
            {
                0 => 0.0,
                1 => 1.0,
                _ => random.NextDouble(),
            };
            var lower = new double[genomeSize];
            var upper = new double[genomeSize];
            for (var j = 0; j < genomeSize; j++)
            {
                lower[j] = -10.0 + 20.0 * random.NextDouble();
                upper[j] = lower[j] + 0.5 + 10.0 * random.NextDouble();
            }

            var population = new double[populationSize * genomeSize];
            for (var k = 0; k < population.Length; k++)
            {
                var j = k % genomeSize;
                population[k] = lower[j] + random.NextDouble() * (upper[j] - lower[j]);
            }

            var individual = random.Next(populationSize);
            var recorder = new RecordingRandomProvider(1000 + testCase);
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
            };
            new MutationStrategy(mutationForce, crossoverProbability).Mutate(in context);

            var draws = new ScriptedDraws(recorder.Calls);
            var gpuTrial = step.BuildTrial(ref draws, individual, mutationForce, crossoverProbability, population, lower, upper);

            Assert.True(recorder.Calls.Count == draws.Consumed, $"case {testCase}: the CPU drew {recorder.Calls.Count} times, the GPU {draws.Consumed}");
            for (var j = 0; j < genomeSize; j++)
            {
                Assert.True(
                    BitConverter.DoubleToInt64Bits(cpuTrial[j]) == BitConverter.DoubleToInt64Bits(gpuTrial[j]),
                    $"case {testCase}, gene {j}: CPU {cpuTrial[j]:R}, GPU {gpuTrial[j]:R}");
            }

            var unbounded = new ScriptedDraws(recorder.Calls);
            var unrepaired = step.BuildTrial(
                ref unbounded,
                individual,
                mutationForce,
                crossoverProbability,
                population,
                [.. Enumerable.Repeat(double.MinValue, genomeSize)],
                [.. Enumerable.Repeat(double.MaxValue, genomeSize)]);
            for (var j = 0; j < genomeSize; j++)
            {
                repairedBelow += unrepaired[j] < lower[j] ? 1 : 0;
                repairedAbove += unrepaired[j] > upper[j] ? 1 : 0;
            }
        }

        Assert.True(repairedBelow > 0 && repairedAbove > 0, $"repairs below {repairedBelow}, above {repairedAbove}");
    }
}
