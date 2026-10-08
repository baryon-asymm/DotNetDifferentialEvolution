using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.Tests.Common.Fakes;

namespace DotNetDifferentialEvolution.UnitTests.Algorithms;

/// <summary>
/// The memory update of 6.0.0, as it stood at <c>a5e579e</c>: the weights summed as they are, with
/// no scale. It is the reference the scaled update must equal bit for bit whenever no weight
/// exceeds <c>double.MaxValue / (2 * N)</c> (criterion O2 of <c>Algorithms/Shade</c>), copied here
/// once and never changed; do not "keep it in step" with <see cref="ShadeStrategy"/>.
/// </summary>
/// <remarks>
/// It also holds what O2 needs around the arithmetic: the exact read-back of a strategy's memory
/// through <see cref="ShadeStrategy.GetControlParameters"/>, the random record sets, and the
/// comparison of a strategy with this reference over two generations.
/// </remarks>
internal sealed class UnscaledShadeMemory
{
    private const double TerminalCrValue = -1.0;
    private const int SetCount = 200;
    private const int GenerationsPerSet = 2;

    private readonly bool _useTerminalCr;
    private readonly bool _useLehmerCrMean;
    private readonly int _memorySize;
    private readonly double[] _memoryCr;
    private readonly double[] _memoryF;

    private int _memoryIndex;

    /// <summary>
    /// Initializes the reference memory with the same value in every slot.
    /// </summary>
    /// <param name="memorySize">The number of slots (H).</param>
    /// <param name="initialMemoryValue">The value every slot starts with.</param>
    /// <param name="useTerminalCr">Whether the L-SHADE terminal rule is on.</param>
    /// <param name="useLehmerCrMean">Whether <c>M_CR</c> takes the weighted Lehmer mean.</param>
    public UnscaledShadeMemory(
        int memorySize,
        double initialMemoryValue,
        bool useTerminalCr,
        bool useLehmerCrMean)
    {
        _memorySize = memorySize;
        _useTerminalCr = useTerminalCr;
        _useLehmerCrMean = useLehmerCrMean;
        _memoryCr = new double[memorySize];
        _memoryF = new double[memorySize];
        Array.Fill(_memoryCr, initialMemoryValue);
        Array.Fill(_memoryF, initialMemoryValue);
    }

    /// <summary>
    /// Runs the comparison of O2: from 200 random sets of two generations of records, the memory of
    /// the strategy equals this reference's, slot by slot, bit for bit.
    /// </summary>
    /// <param name="seed">The seed of the record sets.</param>
    /// <param name="useTerminalCr">Whether the reference applies the terminal rule.</param>
    /// <param name="useLehmerCrMean">Whether the reference takes the Lehmer mean for <c>M_CR</c>.</param>
    /// <param name="allZeroCrInEveryFourthSet">
    /// Whether every fourth set (0, 4, 8, ...) is four improving records whose CR are all zero.
    /// </param>
    /// <param name="createStrategy">Makes the strategy under test from a population size and a memory size.</param>
    /// <param name="createContext">Makes the context of a population size.</param>
    public static void AssertTheStrategyEqualsTheUnscaledArithmetic(
        int seed,
        bool useTerminalCr,
        bool useLehmerCrMean,
        bool allZeroCrInEveryFourthSet,
        Func<int, int, ShadeStrategy> createStrategy,
        Func<int, ProblemContext> createContext)
    {
        ArgumentNullException.ThrowIfNull(createStrategy);
        ArgumentNullException.ThrowIfNull(createContext);

        var random = new DeterministicRandomProvider(seed);

        for (var set = 0; set < SetCount; set++)
        {
            var allZeroCr = allZeroCrInEveryFourthSet && set % 4 == 0;
            var populationSize = allZeroCr ? 4 : 4 + random.Next(5);
            var memorySize = allZeroCr ? 1 : 1 + set % 3;

            var strategy = createStrategy(populationSize, memorySize);
            var context = createContext(populationSize);
            var reference = new UnscaledShadeMemory(memorySize, 0.5, useTerminalCr, useLehmerCrMean);

            for (var generation = 0; generation < GenerationsPerSet; generation++)
            {
                var records = RandomRecords(random, populationSize, allZeroCr);
                strategy.AfterGeneration(new GenerationContext(context), records);
                reference.Update(records, populationSize);
            }

            for (var slot = 0; slot < memorySize; slot++)
            {
                ReadBack(strategy, slot, reference.IsTerminal(slot), out var f, out var cr);

                var where = $"set {set}, slot {slot} of {memorySize}, N = {populationSize}";
                Assert.True(
                    BitConverter.DoubleToInt64Bits(reference.ReadableF(slot)) == BitConverter.DoubleToInt64Bits(f),
                    $"F differs ({where}): reference {reference.ReadableF(slot):R}, strategy {f:R}");
                Assert.True(
                    BitConverter.DoubleToInt64Bits(reference.ReadableCr(slot)) == BitConverter.DoubleToInt64Bits(cr),
                    $"CR differs ({where}): reference {reference.ReadableCr(slot):R}, strategy {cr:R}");
            }
        }
    }

    /// <summary>
    /// Reads one memory slot of a strategy back exactly through its public sampling path. The slot is
    /// drawn as the slot; the Gaussian's two uniforms are 0, which makes its standard normal exactly
    /// zero (<c>u1 = 1</c>, <c>ln 1 = 0</c>), and the Cauchy's uniform is ½, which makes its tangent
    /// zero, so both samplers return their centre without rounding. A terminal slot draws no
    /// Gaussian.
    /// </summary>
    /// <param name="strategy">The strategy whose memory is read.</param>
    /// <param name="slot">The memory slot.</param>
    /// <param name="terminal">Whether the slot is terminal, so that only the Cauchy is drawn.</param>
    /// <param name="f">The slot's <c>M_F</c>, as the sampler returns it (at most 1).</param>
    /// <param name="cr">The slot's <c>M_CR</c>, as the sampler returns it (0 when terminal).</param>
    public static void ReadBack(
        ShadeStrategy strategy,
        int slot,
        bool terminal,
        out double f,
        out double cr)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        double[] doubles = terminal ? [0.5] : [0.0, 0.0, 0.5];
        var draws = new ScriptedRandomProvider(ints: [slot], doubles: doubles);
        strategy.GetControlParameters(0, draws, out f, out cr);
    }

    /// <summary>
    /// Whether the slot holds the terminal sentinel.
    /// </summary>
    /// <param name="slot">The memory slot.</param>
    /// <returns><see langword="true"/> when <c>M_CR</c> of the slot is the terminal value.</returns>
    public bool IsTerminal(
        int slot) => _memoryCr[slot] < 0.0;

    /// <summary>
    /// The <c>M_F</c> of a slot as the sampler returns it at the Cauchy's centre.
    /// </summary>
    /// <param name="slot">The memory slot.</param>
    /// <returns>The memory value, capped at 1 as the sampler caps it.</returns>
    public double ReadableF(
        int slot) => Math.Min(_memoryF[slot], 1.0);

    /// <summary>
    /// The <c>M_CR</c> of a slot as the sampler returns it at the Gaussian's centre.
    /// </summary>
    /// <param name="slot">The memory slot.</param>
    /// <returns>The memory value clamped to [0, 1]; 0 for a terminal slot.</returns>
    public double ReadableCr(
        int slot) => _memoryCr[slot] < 0.0 ? 0.0 : Math.Clamp(_memoryCr[slot], 0.0, 1.0);

    /// <summary>
    /// The 6.0.0 memory update: weights summed unscaled.
    /// </summary>
    /// <param name="trialRecords">The generation's records.</param>
    /// <param name="currentPopulationSize">The number of live individuals.</param>
    public void Update(
        ReadOnlySpan<TrialRecord> trialRecords,
        int currentPopulationSize)
    {
        var weightSum = 0.0;
        var weightedCrSum = 0.0;
        var weightedCrSquaredSum = 0.0;
        var weightedFSum = 0.0;
        var weightedFSquaredSum = 0.0;
        var maxSuccessfulCr = 0.0;

        for (var i = 0; i < currentPopulationSize; i++)
        {
            if (!trialRecords[i].Improved)
            {
                continue;
            }

            var weight = trialRecords[i].ParentFfValue - trialRecords[i].TrialFfValue;
            if (!double.IsFinite(weight))
            {
                continue;
            }

            var cr = trialRecords[i].UsedCr;
            var f = trialRecords[i].UsedF;

            weightSum += weight;
            weightedCrSum += weight * cr;
            weightedCrSquaredSum += weight * cr * cr;
            weightedFSum += weight * f;
            weightedFSquaredSum += weight * f * f;
            if (cr > maxSuccessfulCr)
            {
                maxSuccessfulCr = cr;
            }
        }

        if (weightSum <= 0.0)
        {
            return;
        }

        var isTerminal = _useTerminalCr && (_memoryCr[_memoryIndex] < 0.0 || maxSuccessfulCr <= 0.0);

        var crMean = _useLehmerCrMean && weightedCrSum > 0.0
            ? weightedCrSquaredSum / weightedCrSum
            : weightedCrSum / weightSum;

        _memoryCr[_memoryIndex] = isTerminal ? TerminalCrValue : crMean;

        if (weightedFSum > 0.0)
        {
            _memoryF[_memoryIndex] = weightedFSquaredSum / weightedFSum;
        }

        _memoryIndex = (_memoryIndex + 1) % _memorySize;
    }

    // One generation of records. Weights run from tiny to 1e305 (all below the bound for the
    // population sizes used here); a tenth of the records are kept parents, and the rest of the
    // oddities are the ones the memory has to survive: a tie accepted without improving, an
    // improvement over a NaN or an infinite parent, an "improvement" of exactly zero.
    private static TrialRecord[] RandomRecords(
        DeterministicRandomProvider random,
        int populationSize,
        bool allImprovingWithZeroCr)
    {
        var records = new TrialRecord[populationSize];
        for (var i = 0; i < populationSize; i++)
        {
            var kind = allImprovingWithZeroCr ? 9 : random.Next(10);
            var weight = Math.Pow(10.0, -300.0 + 605.0 * random.NextDouble());
            var usedCr = allImprovingWithZeroCr || random.Next(6) == 0 ? 0.0 : random.NextDouble();
            var usedF = random.Next(8) == 0 ? 1.0 : 0.05 + 0.95 * random.NextDouble();

            records[i] = kind switch
            {
                0 => new TrialRecord { Outcome = SelectionOutcome.ParentKept, UsedCr = usedCr, UsedF = usedF },
                1 => new TrialRecord { Outcome = SelectionOutcome.TrialAccepted, ParentFfValue = 3.0, TrialFfValue = 3.0, UsedCr = usedCr, UsedF = usedF },
                2 => Improved(double.NaN, 3.0, usedCr, usedF),
                3 => Improved(double.PositiveInfinity, 3.0, usedCr, usedF),
                4 => Improved(3.0, 3.0, usedCr, usedF),
                _ => Improved(weight, 0.0, usedCr, usedF),
            };
        }

        return records;
    }

    private static TrialRecord Improved(
        double parentFfValue,
        double trialFfValue,
        double usedCr,
        double usedF)
    {
        return new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = parentFfValue,
            TrialFfValue = trialFfValue,
            UsedCr = usedCr,
            UsedF = usedF,
        };
    }
}
