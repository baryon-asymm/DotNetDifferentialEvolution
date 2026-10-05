using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using DotNetDifferentialEvolution.GPU.Test.Kernels;
using DotNetDifferentialEvolution.RandomProviders;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// ACCEPTANCE.md, check S8: the archive.
/// <list type="bullet">
/// <item>The package's rule (<see cref="ArchiveRules.SlotOf"/>, fill positions from the size before plus the improved
/// parents before, the later parent kept on a shared slot) equals the CPU package's <c>UpdateArchive</c>, run through
/// <see cref="JadeStrategy.AfterGeneration"/> on a recording provider, given the same slot draws in index order: archive
/// and size bit for bit over 200 random cases that fill, fill into overflow, start full with collisions, or have no
/// capacity.</item>
/// <item>The device kernels equal the same rule given each parent's own stream-1 draws, bit for bit, across several
/// chunks.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public class ArchiveParityTests
{
    private const int CaseCount = 200;
    private const int CaseSeed = 20261009;

    /// <summary>The rule equals the CPU package's loop given the same draws.</summary>
    [Fact]
    public void TheRuleEqualsTheCpuPackagesLoop()
    {
        var random = new SeededRandomProvider(CaseSeed);
        var kinds = new int[4];
        for (var testCase = 0; testCase < CaseCount; testCase++)
        {
            var populationSize = 4 + random.Next(57);
            var genomeSize = 1 + random.Next(5);
            var capacity = (testCase % 4) switch
            {
                0 => 2 * populationSize,
                1 => 1 + random.Next(populationSize),
                2 => 1 + random.Next(3),
                _ => 0,
            };
            var sizeBefore = testCase % 4 == 2 ? capacity : random.Next(capacity + 1);
            var initial = Values(random, capacity * genomeSize);
            var parents = Values(random, populationSize * genomeSize);
            var outcomes = Outcomes(random, populationSize);

            var recorder = new RecordingRandomProvider(4000 + testCase);
            var jade = new JadeStrategy(populationSize);
            jade.UseRandomProvider(recorder);
            var cpuArchive = (double[])initial.Clone();
            var (context, problem) = CpuGeneration.Context(parents, new double[populationSize], genomeSize, cpuArchive, capacity, sizeBefore);
            jade.AfterGeneration(context, [.. outcomes.Select(outcome => CpuGeneration.Record(outcome, 0.5, 0.5, 1.0, 0.0))]);

            ScriptedDraws[] script = [new ScriptedDraws(recorder.Calls)];
            var hostArchive = (double[])initial.Clone();
            var hostSize = HostArchive.Update(hostArchive, sizeBefore, capacity, genomeSize, parents, outcomes, _ => ref script[0]);

            Assert.True(recorder.Calls.Count == script[0].Consumed, $"case {testCase}: the CPU drew {recorder.Calls.Count} slots, the rule {script[0].Consumed}");
            Assert.True(problem.ArchiveSize == hostSize, $"case {testCase}: size CPU {problem.ArchiveSize}, rule {hostSize}");
            ParityCases.AssertSameBits(cpuArchive, hostArchive, $"case {testCase}");
            kinds[HostArchive.Kind(sizeBefore, capacity, outcomes)]++;
        }

        Assert.True(kinds.All(count => count > 0), $"fill {kinds[0]}, into overflow {kinds[1]}, full {kinds[2]}, none {kinds[3]}");
    }

    /// <summary>The device kernels equal the rule given each parent's stream-1 draws.</summary>
    /// <param name="populationSize">N.</param>
    /// <param name="capacity">The capacity.</param>
    /// <param name="sizeBefore">The size before the generation.</param>
    [Theory]
    [InlineData(5, 10, 0)]
    [InlineData(100, 30, 12)]
    [InlineData(100, 3, 3)]
    [InlineData(1500, 2000, 700)]
    [InlineData(3000, 7, 7)]
    public void TheDeviceKernelsEqualTheRule(int populationSize, int capacity, int sizeBefore)
    {
        const int genomeSize = 3;
        const int seed = 5;
        const int generation = 17;
        var random = new SeededRandomProvider(CaseSeed + populationSize + capacity);
        var initial = Values(random, capacity * genomeSize);
        var parents = Values(random, populationSize * genomeSize);
        var outcomes = Outcomes(random, populationSize);
        using var step = new HostStep();
        var accelerator = step.Accelerator;
        using var bookkeeping = new GenerationBookkeeping(
            accelerator,
            new BookkeepingPlan(populationSize, genomeSize, SchemeKind.CurrentToPBest, ParameterRule.Jade, capacity, 0, 0.1, false, double.NaN, double.NaN, null),
            seed);
        using var parentBuffer = step.Upload(parents);
        using var fitness = step.Upload(new double[populationSize]);
        using var unused = step.Upload([0.0]);
        bookkeeping.Views.Archive.CopyFromCPU(initial);
        bookkeeping.Views.ArchiveSize.CopyFromCPU([sizeBefore, 0]);
        bookkeeping.Views.Outcomes.CopyFromCPU(outcomes);
        var views = new PopulationViews(unused.View, fitness.View, parentBuffer.View, fitness.View, unused.View, unused.View, unused.View);

        bookkeeping.AfterGeneration(ref views, generation, populationSize, capacity, populationSize, capacity);
        accelerator.Synchronize();
        var deviceArchive = new double[initial.Length];
        var deviceSize = new int[2];
        bookkeeping.Views.Archive.CopyToCPU(deviceArchive);
        bookkeeping.Views.ArchiveSize.CopyToCPU(deviceSize);

        var hostArchive = (double[])initial.Clone();
        var streams = new PhiloxDraws[populationSize];
        var hostSize = HostArchive.Update(
            hostArchive,
            sizeBefore,
            capacity,
            genomeSize,
            parents,
            outcomes,
            i =>
            {
                streams[i] = new PhiloxDraws(seed, i, generation, PhiloxDraws.ArchiveStream);
                return ref streams[i];
            });
        Assert.Equal(hostSize, deviceSize[0]);
        ParityCases.AssertSameBits(hostArchive, deviceArchive, $"N {populationSize}, capacity {capacity}, size {sizeBefore}");
    }

    private static double[] Values(SeededRandomProvider random, int count)
    {
        var values = new double[count];
        for (var k = 0; k < count; k++)
        {
            values[k] = random.NextDouble();
        }

        return values;
    }

    /// <summary>Outcomes with a share of improved trials drawn per case, from none to nearly all.</summary>
    private static int[] Outcomes(SeededRandomProvider random, int populationSize)
    {
        var share = random.NextDouble();
        var outcomes = new int[populationSize];
        for (var i = 0; i < populationSize; i++)
        {
            outcomes[i] = random.NextDouble() < share ? Selection.Improved : random.Next(2);
        }

        return outcomes;
    }
}
