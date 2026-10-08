using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Test.Kernels;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.RandomProviders;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>
/// Bookkeeping/ACCEPTANCE.md of the GPU package, check S7: JADE's means and the SHADE and L-SHADE memories after two generations of trial records.
/// <list type="bullet">
/// <item>The package's rules (<see cref="SuccessSums"/>, <see cref="AdaptationRules"/>), summed in one chunk as the
/// device sums N ≤ 1 024, equal the CPU strategies after <c>AfterGeneration</c> with the same records, bit for bit, on 200
/// random sets (N in [4, 1 024]; improved, accepted and kept; infinite and <see cref="double.NaN"/> parents; a first
/// generation with no success in every tenth set; all-zero successful CR in some L-SHADE sets). The CPU state is read
/// through <c>GetControlParameters</c> with draws that return it exactly (<c>ReadBack</c>).</item>
/// <item>The device kernels equal the same rules summed in chunks of 1 024, bit for bit, for N up to 5 000, over three
/// generations of which the first has no success: the memory index must not move then.</item>
/// <item>Check S19, the same two cases with parents scored <see cref="double.MaxValue"/> ("WhenTheWeightsOverflow"): one
/// parent in four for the CPU parity, one in 700 for the device at N above 1 024 (so most chunks hold none) and one in two
/// below it. The host composition applies <see cref="AdaptationRules.ScaleOf"/> from the generation's largest weight, and
/// every memory value must be finite.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public class AdaptationParityTests
{
    private const int SetCount = 200;
    private const int CaseSeed = 20261008;

    /// <summary>The package's rules equal the CPU strategies after two generations, bit for bit.</summary>
    [Fact]
    public void TheRulesEqualTheCpuStrategies() => AssertTheRulesEqualTheCpuStrategies(CaseSeed, sentinelOneIn: 0);

    /// <summary>S19: with one parent in four scored <see cref="double.MaxValue"/> the rules still equal the CPU strategies bit for bit, and every value is finite.</summary>
    [Fact]
    public void TheRulesEqualTheCpuStrategiesWhenTheWeightsOverflow() => AssertTheRulesEqualTheCpuStrategies(CaseSeed + 19, sentinelOneIn: 4);

    private static void AssertTheRulesEqualTheCpuStrategies(int seed, int sentinelOneIn)
    {
        var random = new SeededRandomProvider(seed);
        var noSuccessFirst = 0;
        var scaledGenerations = 0;
        for (var set = 0; set < SetCount; set++)
        {
            var rule = (set % 3) switch { 0 => ParameterRule.Jade, 1 => ParameterRule.Shade, _ => ParameterRule.Shade };
            var lShade = set % 3 == 2;
            var populationSize = 4 + random.Next(1021);
            var memorySize = lShade ? 6 : 1 + random.Next(10);
            var adaptationRate = random.NextDouble();
            var initial = lShade ? AdaptationRules.InitialValue : 0.01 + 0.98 * random.NextDouble();
            var generations = new[]
            {
                Records(random, populationSize, noSuccess: set % 10 == 0, zeroCr: lShade && set % 4 == 0, sentinelOneIn),
                Records(random, populationSize, noSuccess: false, zeroCr: lShade && set % 8 == 0, sentinelOneIn),
            };
            noSuccessFirst += set % 10 == 0 ? 1 : 0;

            IGenerationStrategy cpu = rule == ParameterRule.Jade
                ? new JadeStrategy(populationSize, adaptationRate, initial)
                : lShade
                    ? new LShadeStrategy(populationSize, long.MaxValue, 0.0, memorySize)
                    : new ShadeStrategy(populationSize, memorySize, initial);
            var state = new HostState(rule, lShade, memorySize, adaptationRate, initial);
            foreach (var records in generations)
            {
                var (context, _) = CpuGeneration.Context(
                    new double[populationSize], [.. records.Select(record => record.ParentFfValue)], 1, [], 0, 0);
                cpu.AfterGeneration(context, records);
                state.Apply(records, chunkSize: populationSize);
            }

            var what = $"set {set} ({(lShade ? "L-SHADE" : rule.ToString())}, N {populationSize})";
            state.AssertEquals((IControlParameterProvider)cpu, what);
            if (sentinelOneIn > 0)
            {
                state.AssertAllFinite(what);
            }

            scaledGenerations += state.ScaledGenerations;
        }

        Assert.True(noSuccessFirst > 0, "no set began with a generation without success");
        Assert.Equal(sentinelOneIn > 0, scaledGenerations > 0);
    }

    /// <summary>The device kernels equal the rules summed in chunks of 1 024, bit for bit, across several chunks and after a generation without success.</summary>
    /// <param name="ruleName">JADE or SHADE, by name.</param>
    /// <param name="lShade">Whether L-SHADE's memory update applies.</param>
    /// <param name="populationSize">N.</param>
    [Theory]
    [InlineData(nameof(ParameterRule.Jade), false, 1)]
    [InlineData(nameof(ParameterRule.Jade), false, 1024)]
    [InlineData(nameof(ParameterRule.Jade), false, 5000)]
    [InlineData(nameof(ParameterRule.Shade), false, 1025)]
    [InlineData(nameof(ParameterRule.Shade), false, 3000)]
    [InlineData(nameof(ParameterRule.Shade), true, 4)]
    [InlineData(nameof(ParameterRule.Shade), true, 5000)]
    public void TheDeviceKernelsEqualTheRules(string ruleName, bool lShade, int populationSize) =>
        AssertTheDeviceKernelsEqualTheRules(ruleName, lShade, populationSize, CaseSeed + populationSize, sentinelOneIn: 0);

    /// <summary>
    /// S19: with parents scored <see cref="double.MaxValue"/> (one in 700 above N = 1 024, so most chunks hold none; one in
    /// two below it) the device kernels equal the rules with the scale applied, bit for bit, and every value is finite.
    /// </summary>
    /// <param name="ruleName">JADE or SHADE, by name.</param>
    /// <param name="lShade">Whether L-SHADE's memory update applies.</param>
    /// <param name="populationSize">N.</param>
    [Theory]
    [InlineData(nameof(ParameterRule.Jade), false, 5000)]
    [InlineData(nameof(ParameterRule.Shade), false, 1025)]
    [InlineData(nameof(ParameterRule.Shade), false, 3000)]
    [InlineData(nameof(ParameterRule.Shade), true, 4)]
    [InlineData(nameof(ParameterRule.Shade), true, 5000)]
    public void TheDeviceKernelsEqualTheRulesWhenTheWeightsOverflow(string ruleName, bool lShade, int populationSize) =>
        AssertTheDeviceKernelsEqualTheRules(
            ruleName, lShade, populationSize, CaseSeed + 19 + populationSize, sentinelOneIn: populationSize > BookkeepingKernels.ChunkSize ? 700 : 2);

    private static void AssertTheDeviceKernelsEqualTheRules(string ruleName, bool lShade, int populationSize, int seed, int sentinelOneIn)
    {
        var rule = Enum.Parse<ParameterRule>(ruleName);
        var random = new SeededRandomProvider(seed);
        const int memorySize = 5;
        const double adaptationRate = 0.3;
        using var step = new HostStep();
        var accelerator = step.Accelerator;
        using var bookkeeping = new GenerationBookkeeping(
            accelerator,
            new BookkeepingPlan(populationSize, 1, SchemeKind.CurrentToPBest, rule, 0, memorySize, adaptationRate, lShade, double.NaN, double.NaN, null),
            seed: 1);
        using var parents = accelerator.Allocate1D<double>(populationSize);
        using var survivors = accelerator.Allocate1D<double>(populationSize);
        using var genes = accelerator.Allocate1D<double>(populationSize);
        var state = new HostState(rule, lShade, memorySize, adaptationRate, AdaptationRules.InitialValue);
        for (var generation = 1; generation <= 3; generation++)
        {
            var records = Records(random, populationSize, noSuccess: generation == 1, zeroCr: false, sentinelOneIn);
            bookkeeping.Views.Outcomes.CopyFromCPU([.. records.Select(Outcome)]);
            bookkeeping.Views.MutationForces.CopyFromCPU([.. records.Select(record => record.UsedF)]);
            bookkeeping.Views.CrossoverProbabilities.CopyFromCPU([.. records.Select(record => record.UsedCr)]);
            parents.View.CopyFromCPU([.. records.Select(record => record.ParentFfValue)]);
            survivors.View.CopyFromCPU([.. records.Select(record => record.Improved ? record.TrialFfValue : record.ParentFfValue)]);
            var views = new PopulationViews(genes.View, survivors.View, genes.View, parents.View, genes.View, genes.View, genes.View);
            bookkeeping.AfterGeneration(ref views, generation, populationSize, 0, populationSize, 0);
            state.Apply(records, BookkeepingKernels.ChunkSize);
        }

        accelerator.Synchronize();
        var adaptation = new double[bookkeeping.Views.Adaptation.IntLength];
        bookkeeping.Views.Adaptation.CopyToCPU(adaptation);
        var what = $"{(lShade ? "L-SHADE" : rule.ToString())}, N {populationSize}";
        state.AssertEquals(adaptation, what);
        if (sentinelOneIn > 0)
        {
            Assert.All(adaptation, value => Assert.True(double.IsFinite(value), $"{what}: a memory value is {value:R}"));
            Assert.Equal(rule == ParameterRule.Shade, state.ScaledGenerations > 0);
        }
    }

    /// <summary>
    /// Random trial records: F in [0.05, 1), CR in [0, 1) (or 0 for every success), parents in [0, 10) with one in fifty
    /// <see cref="double.NaN"/> or +∞; an improved trial below its parent, an accepted one equal, a kept one above. With
    /// <paramref name="sentinelOneIn"/> above 0, one parent in that many is scored <see cref="double.MaxValue"/>, as an
    /// objective scores an infeasible point: an improved trial over it is finite and well below it (a tiny value, or up to
    /// half of it), so that its weight does not round to 0 and its improvement is near <see cref="double.MaxValue"/>.
    /// </summary>
    private static TrialRecord[] Records(SeededRandomProvider random, int populationSize, bool noSuccess, bool zeroCr, int sentinelOneIn)
    {
        var records = new TrialRecord[populationSize];
        for (var i = 0; i < populationSize; i++)
        {
            var outcome = noSuccess ? random.Next(2) : random.Next(3);
            var parent = random.Next(50) switch
            {
                0 => double.NaN,
                1 => double.PositiveInfinity,
                _ => 10.0 * random.NextDouble(),
            };
            var sentinel = sentinelOneIn > 0 && random.Next(sentinelOneIn) == 0;
            parent = sentinel ? double.MaxValue : parent;
            var trial = outcome switch
            {
                Selection.Improved when sentinel => random.Next(2) == 0 ? 10.0 * random.NextDouble() : 0.5 * double.MaxValue * random.NextDouble(),
                Selection.Improved => double.IsFinite(parent) ? parent - 5.0 * random.NextDouble() - 1e-9 : 1.0,
                Selection.Accepted => parent,
                _ => double.IsFinite(parent) ? parent + random.NextDouble() + 1e-9 : double.NaN,
            };
            var crossoverProbability = zeroCr && outcome == Selection.Improved ? 0.0 : random.NextDouble();
            records[i] = CpuGeneration.Record(outcome, 0.05 + 0.95 * random.NextDouble(), crossoverProbability, parent, trial);
        }

        return records;
    }

    private static int Outcome(TrialRecord record) =>
        record.Improved ? Selection.Improved : record.Replaced ? Selection.Accepted : Selection.Kept;

    /// <summary>JADE's means or SHADE's memory and index, kept on the host by the package's rules.</summary>
    private sealed class HostState(ParameterRule rule, bool lShade, int memorySize, double adaptationRate, double initial)
    {
        private readonly ParameterRule _rule = rule;
        private readonly bool _lShade = lShade;
        private readonly int _memorySize = memorySize;
        private readonly double _adaptationRate = adaptationRate;
        private readonly double[] _values = [.. Enumerable.Repeat(initial, rule == ParameterRule.Jade ? 2 : 2 * memorySize)];
        private int _memoryIndex;

        /// <summary>Gets the number of generations applied so far whose weights were divided by a scale above 1.</summary>
        public int ScaledGenerations { get; private set; }

        /// <summary>
        /// One generation: the generation's largest weight and its scale (<see cref="AdaptationRules.ScaleOf"/>, SHADE only),
        /// the sums of the weights over the scale in index order within chunks, the chunks in order, then the update, as the
        /// kernels do.
        /// </summary>
        public void Apply(TrialRecord[] records, int chunkSize)
        {
            var largest = 0.0;
            if (_rule == ParameterRule.Shade)
            {
                foreach (var record in records)
                {
                    var weight = AdaptationRules.WeightOf(_rule, Outcome(record), record.ParentFfValue, record.TrialFfValue);
                    largest = weight > largest ? weight : largest;
                }
            }

            var scale = _rule == ParameterRule.Shade ? AdaptationRules.ScaleOf(largest, records.Length) : 1.0;
            ScaledGenerations += scale > 1.0 ? 1 : 0;
            var total = default(SuccessSums);
            for (var first = 0; first < records.Length; first += chunkSize)
            {
                var chunk = default(SuccessSums);
                for (var i = first; i < Math.Min(first + chunkSize, records.Length); i++)
                {
                    var weight = AdaptationRules.WeightOf(_rule, Outcome(records[i]), records[i].ParentFfValue, records[i].TrialFfValue);
                    if (!double.IsNaN(weight))
                    {
                        chunk = chunk.Add(weight / scale, records[i].UsedCr, records[i].UsedF);
                    }
                }

                total = total.Combine(chunk);
            }

            if (_rule == ParameterRule.Jade)
            {
                AdaptationRules.UpdateJadeMeans(total, _adaptationRate, ref _values[0], ref _values[1]);
            }
            else if (AdaptationRules.UpdateShadeSlot(total, _lShade, ref _values[_memoryIndex], ref _values[_memorySize + _memoryIndex]))
            {
                _memoryIndex = (_memoryIndex + 1) % _memorySize;
            }
        }

        /// <summary>Asserts every value is finite.</summary>
        /// <param name="what">The case, for the message.</param>
        public void AssertAllFinite(string what)
            => Assert.All(_values, value => Assert.True(double.IsFinite(value), $"{what}: a memory value is {value:R}"));

        /// <summary>Asserts the device's buffer holds these values bit for bit.</summary>
        public void AssertEquals(double[] device, string what)
        {
            Assert.Equal(_values.Length, device.Length);
            for (var k = 0; k < _values.Length; k++)
            {
                ParityCases.AssertSameBits(_values[k], device[k], $"{what}, entry {k}");
            }
        }

        /// <summary>Asserts the CPU strategy holds these values bit for bit, read back through its control parameters.</summary>
        public void AssertEquals(IControlParameterProvider cpu, string what)
        {
            var slots = _rule == ParameterRule.Jade ? 1 : _memorySize;
            for (var k = 0; k < slots; k++)
            {
                var (crossoverProbability, mutationForce) = ReadBack(cpu, k);
                var expectedCr = _rule == ParameterRule.Jade ? _values[0] : _values[k];
                var expectedF = _rule == ParameterRule.Jade ? _values[1] : _values[_memorySize + k];
                ParityCases.AssertSameBits(expectedCr < 0.0 ? 0.0 : expectedCr, crossoverProbability, $"{what}, CR of slot {k}");
                ParityCases.AssertSameBits(expectedF, mutationForce, $"{what}, F of slot {k}");
            }
        }

        /// <summary>
        /// A CPU strategy's CR and F at a slot, exactly: the slot drawn as <paramref name="slot"/>, the Gaussian's first
        /// uniform 1 (so its normal is ±0) and the Cauchy's uniform ½ (so its tangent is 0). A terminal slot draws no
        /// Gaussian; its Cauchy then takes the two zeros, each a tangent of −π/2 and a redraw, and then ½.
        /// </summary>
        private static (double Cr, double F) ReadBack(IControlParameterProvider cpu, int slot)
        {
            var provider = new CpuGeneration.ScriptedProvider([slot], [0.0, 0.0, 0.5, 0.5]);
            cpu.GetControlParameters(0, provider, out var mutationForce, out var crossoverProbability);
            return (crossoverProbability, mutationForce);
        }
    }
}
