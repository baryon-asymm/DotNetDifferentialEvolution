using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check S4: the same draws give the same F and CR, bit for bit and from the same draws, through the
/// GPU's <see cref="ControlParameters"/> and the CPU package's <c>GetControlParameters</c> of <see cref="JdeStrategy"/>,
/// <see cref="JadeStrategy"/>, <see cref="ShadeStrategy"/> and <see cref="LShadeStrategy"/>. 10⁴ draws each: 100 random
/// states of 100 draws, the CPU on a <see cref="RecordingRandomProvider"/> replayed into a <see cref="ScriptedDraws"/>.
/// The states include a terminal (negative) SHADE slot; a Cauchy redraw (F ≤ 0), F cut to 1 and a terminal slot each
/// occur, counted and asserted.
/// </summary>
[Trait("Category", "Unit")]
public class ControlParameterParityTests
{
    private const int StateCount = 100;
    private const int DrawsPerState = 100;
    private const int CaseSeed = 20261007;

    /// <summary>jDE: the regenerated or inherited F and CR are the CPU's.</summary>
    [Fact]
    public void JdeDrawsTheCpuPackagesParameters()
    {
        var random = new SeededRandomProvider(CaseSeed);
        var regenerated = 0;
        for (var state = 0; state < StateCount; state++)
        {
            var currentF = 0.1 + 0.9 * random.NextDouble();
            var currentCr = random.NextDouble();
            var cpu = new JdeStrategy(populationSize: 1, initialMutationForce: currentF, initialCrossoverProbability: currentCr);
            for (var k = 0; k < DrawsPerState; k++)
            {
                var (recorder, cpuF, cpuCr) = OnCpu(cpu, state * DrawsPerState + k);
                var draws = new ScriptedDraws(recorder.Calls);
                ControlParameters.Jde(ref draws, currentF, currentCr, out var gpuF, out var gpuCr);

                AssertSame(recorder, draws, cpuF, gpuF, cpuCr, gpuCr, $"jDE state {state}, draw {k}");
                regenerated += BitConverter.DoubleToInt64Bits(gpuF) != BitConverter.DoubleToInt64Bits(currentF) ? 1 : 0;
            }
        }

        Assert.True(regenerated > 0, "jDE never regenerated F");
    }

    /// <summary>JADE: CR from N(μCR, 0.1) clamped, F from Cauchy(μF, 0.1) redrawn while at most 0 and cut to 1, are the CPU's.</summary>
    [Fact]
    public void JadeDrawsTheCpuPackagesParameters()
    {
        var random = new SeededRandomProvider(CaseSeed + 1);
        var counts = new Counts();
        for (var state = 0; state < StateCount; state++)
        {
            var mean = state % 5 == 0 ? 0.01 : state % 5 == 1 ? 0.99 : random.NextDouble();
            var cpu = new JadeStrategy(populationSize: 1, initialMean: mean);
            for (var k = 0; k < DrawsPerState; k++)
            {
                var (recorder, cpuF, cpuCr) = OnCpu(cpu, state * DrawsPerState + k);
                var draws = new ScriptedDraws(recorder.Calls);
                ControlParameters.Jade(ref draws, mean, mean, out var gpuF, out var gpuCr);

                AssertSame(recorder, draws, cpuF, gpuF, cpuCr, gpuCr, $"JADE state {state}, draw {k}");
                counts.Add(recorder.Calls.Count > 3, gpuF, terminal: false);
            }
        }

        counts.AssertEachOccurred(expectTerminal: false);
    }

    /// <summary>SHADE: a slot, CR around it (0 for a terminal slot, with no Gaussian draw), F around it, are the CPU's.</summary>
    [Fact]
    public void ShadeDrawsTheCpuPackagesParameters()
    {
        var random = new SeededRandomProvider(CaseSeed + 2);
        using var step = new HostStep();
        var counts = new Counts();
        for (var state = 0; state < StateCount; state++)
        {
            var memorySize = 1 + random.Next(10);
            var value = state % 4 == 0 ? -1.0 : state % 4 == 1 ? 0.99 : random.NextDouble();
            var cpu = new ShadeStrategy(populationSize: 1, memorySize: memorySize, initialMemoryValue: value);
            using var memory = step.Upload([.. Enumerable.Repeat(value, 2 * memorySize)]);
            for (var k = 0; k < DrawsPerState; k++)
            {
                var (recorder, cpuF, cpuCr) = OnCpu(cpu, state * DrawsPerState + k);
                var draws = new ScriptedDraws(recorder.Calls);
                ControlParameters.Shade(ref draws, memory.View, memorySize, out var gpuF, out var gpuCr);

                AssertSame(recorder, draws, cpuF, gpuF, cpuCr, gpuCr, $"SHADE state {state}, draw {k}");
                counts.Add(recorder.Calls.Count > 4, gpuF, terminal: value < 0.0);
            }
        }

        counts.AssertEachOccurred(expectTerminal: true);
    }

    /// <summary>L-SHADE draws as SHADE does, from its own initial memory.</summary>
    [Fact]
    public void LShadeDrawsTheCpuPackagesParameters()
    {
        using var step = new HostStep();
        for (var state = 0; state < StateCount; state++)
        {
            var memorySize = 1 + state % 10;
            var cpu = new LShadeStrategy(initialPopulationSize: 4, maxEvaluationNumber: 1000, archiveSizeRate: 1.0, memorySize: memorySize);
            using var memory = step.Upload([.. Enumerable.Repeat(ShadeStrategy.DefaultInitialMemoryValue, 2 * memorySize)]);
            for (var k = 0; k < DrawsPerState; k++)
            {
                var (recorder, cpuF, cpuCr) = OnCpu(cpu, state * DrawsPerState + k);
                var draws = new ScriptedDraws(recorder.Calls);
                ControlParameters.Shade(ref draws, memory.View, memorySize, out var gpuF, out var gpuCr);

                AssertSame(recorder, draws, cpuF, gpuF, cpuCr, gpuCr, $"L-SHADE state {state}, draw {k}");
            }
        }
    }

    private static (RecordingRandomProvider Recorder, double F, double Cr) OnCpu(IControlParameterProvider provider, int seed)
    {
        var recorder = new RecordingRandomProvider(seed);
        provider.GetControlParameters(0, recorder, out var mutationForce, out var crossoverProbability);
        return (recorder, mutationForce, crossoverProbability);
    }

    private static void AssertSame(
        RecordingRandomProvider recorder, ScriptedDraws draws, double cpuF, double gpuF, double cpuCr, double gpuCr, string what)
    {
        Assert.True(recorder.Calls.Count == draws.Consumed, $"{what}: the CPU drew {recorder.Calls.Count} times, the GPU {draws.Consumed}");
        ParityCases.AssertSameBits(cpuF, gpuF, $"{what}, F");
        ParityCases.AssertSameBits(cpuCr, gpuCr, $"{what}, CR");
    }

    /// <summary>The cases S4 asserts occurred: a Cauchy redraw, F cut to 1, a terminal slot.</summary>
    private sealed class Counts
    {
        private int _redraws;
        private int _cutToOne;
        private int _terminal;

        public void Add(bool redrawn, double mutationForce, bool terminal)
        {
            _redraws += redrawn ? 1 : 0;
            _cutToOne += mutationForce == 1.0 ? 1 : 0;
            _terminal += terminal ? 1 : 0;
        }

        public void AssertEachOccurred(bool expectTerminal)
        {
            Assert.True(_redraws > 0, "no Cauchy redraw occurred");
            Assert.True(_cutToOne > 0, "F was never cut to 1");
            Assert.True(!expectTerminal || _terminal > 0, "no terminal slot was drawn from");
        }
    }
}
