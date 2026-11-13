using DotNetDifferentialEvolution.GPU.Bookkeeping;
using DotNetDifferentialEvolution.GPU.Devices;
using DotNetDifferentialEvolution.GPU.Kernels;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Bookkeeping;

/// <summary>F7: the memory written by device bookkeeping for improvements at the limits of double.</summary>
public class ExtremeImprovementTests
{
    /// <summary>The CI accelerator runs both memory rules.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CpuAcceleratorRetainsExtremeImprovements() => AssertMemory(Backend.Cpu);

    /// <summary>The physical accelerator runs the same bookkeeping kernels on the same inputs.</summary>
    /// <param name="backendName">The explicitly requested hardware backend.</param>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(nameof(Backend.Cuda))]
    [InlineData(nameof(Backend.OpenCL))]
    public void GpuRetainsExtremeImprovements(string backendName) => AssertMemory(Enum.Parse<Backend>(backendName));

    /// <summary>On OpenCL, excluded records at the end of a chunk cannot change its maximum or its raw sums.</summary>
    /// <param name="singleSuccess">One improvement of 4 among finite kept trials, or mixed unusable records across sum chunks.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Gpu")]
    public void OpenClExcludesUnusableRecordsBeforeReducingWeights(bool singleSuccess)
    {
        using var lease = DeviceSelector.Open(Backend.OpenCL);
        var accelerator = lease.Accelerator;
        var count = singleSuccess ? 1000 : BookkeepingKernels.ChunkSize + 1;
        var chunkSize = BookkeepingKernels.WideChunkSizeOf(count);
        var wideCount = BookkeepingKernels.WideChunkCount(count, chunkSize);
        var sumCount = BookkeepingKernels.ChunkCount(count);
        var plan = new BookkeepingPlan(count, 1, SchemeKind.RandOne, ParameterRule.Shade, 0, 1, 0, false, double.NaN, double.NaN, null);
        using var bookkeeping = new GenerationBookkeeping(accelerator, plan, seed: 1);
        var unit = singleSuccess ? 4 : Math.ScaleB(1.0, -42);
        var before = new double[count];
        var after = new double[count];
        var outcomes = new int[count];
        if (singleSuccess)
        {
            Array.Fill(after, unit);
            Array.Fill(outcomes, Selection.Kept);
            before[0] = unit;
            after[0] = 0;
            outcomes[0] = Selection.Improved;
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                // Every complete wide chunk ends with a quiet NaN parent.
                var kind = i % 8;
                outcomes[i] = kind switch { 1 => Selection.Accepted, 2 => Selection.Kept, _ => Selection.Improved };
                before[i] = kind switch { 1 => 0, 3 or 5 => double.PositiveInfinity, 6 or 7 => double.NaN, _ => unit };
                after[i] = kind switch { 2 => 2 * unit, 4 or 5 => double.NegativeInfinity, 6 => double.NaN, _ => 0 };
            }
        }

        bookkeeping.Views.Outcomes.CopyFromCPU(outcomes);
        bookkeeping.Views.MutationForces.CopyFromCPU([.. Enumerable.Repeat(0.5, count)]);
        bookkeeping.Views.CrossoverProbabilities.CopyFromCPU([.. Enumerable.Repeat(0.25, count)]);
        using var parents = accelerator.Allocate1D(before);
        using var trials = accelerator.Allocate1D(after);
        using var maxima = accelerator.Allocate1D<double>(wideCount);
        using var partials = accelerator.Allocate1D<double>(sumCount * SuccessSums.Width);
        maxima.View.CopyFromCPU([.. Enumerable.Repeat(-7.0, wideCount)]);
        partials.View.CopyFromCPU([.. Enumerable.Repeat(-7.0, sumCount * SuccessSums.Width)]);
        using var maximumKernel = KernelLoader.Load(accelerator, typeof(BookkeepingKernels).GetMethod(nameof(BookkeepingKernels.LargestWeights))!, wideCount);
        using var sumKernel = KernelLoader.Load(accelerator, typeof(BookkeepingKernels).GetMethod(nameof(BookkeepingKernels.SumSuccesses))!, sumCount);
        var maximum = maximumKernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ParameterRule, int, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>>>();
        var sum = sumKernel.CreateLauncherDelegate<Action<AcceleratorStream, Index1D, ParameterRule, int, int, StrategyViews, ArrayView<double>, ArrayView<double>, ArrayView<double>, ArrayView<double>>>();
        maximum(accelerator.DefaultStream, wideCount, ParameterRule.Shade, count, chunkSize, bookkeeping.Views, parents.View, trials.View, maxima.View);
        sum(accelerator.DefaultStream, sumCount, ParameterRule.Shade, count, chunkSize, bookkeeping.Views, parents.View, trials.View, maxima.View, partials.View);
        Assert.All(maxima.GetAsArray1D(), (actual, c) => Assert.Equal(singleSuccess && c > 0 ? 0.0 : unit, actual));
        var stored = partials.GetAsArray1D();
        for (var c = 0; c < sumCount; c++)
        {
            var w = !singleSuccess && c == 0 ? 128 * unit : unit;
            double[] expected = [w, w / 4, w / 16, w / 2, w / 4, 0.25];
            Assert.Equal(expected, stored.AsSpan(c * SuccessSums.Width, SuccessSums.Width).ToArray());
        }
    }

    private static void AssertMemory(Backend backend)
    {
        using var lease = DeviceSelector.Open(backend);
        var accelerator = lease.Accelerator;
        const int count = BookkeepingKernels.ChunkSize + 1;
        using var parents = accelerator.Allocate1D<double>(count);
        using var survivors = accelerator.Allocate1D<double>(count);
        using var genes = accelerator.Allocate1D<double>(count);
        var views = new PopulationViews(genes.View, survivors.View, genes.View, parents.View, genes.View, genes.View, genes.View);
        foreach (var lShade in new[] { false, true })
        {
            var plan = new BookkeepingPlan(count, 1, SchemeKind.CurrentToPBest, ParameterRule.Shade, 0, 1, 0, lShade, double.NaN, double.NaN, null);
            using var bookkeeping = new GenerationBookkeeping(accelerator, plan, seed: 1);
            var outcomes = new int[count];
            var forces = new double[count];
            var probabilities = new double[count];
            outcomes[0] = outcomes[1] = outcomes[2] = outcomes[^1] = Selection.Improved;
            forces[0] = probabilities[^1] = 0.25;
            forces[^1] = probabilities[0] = 0.75;
            forces[1] = forces[2] = probabilities[1] = probabilities[2] = 1;
            bookkeeping.Views.Outcomes.CopyFromCPU(outcomes);
            bookkeeping.Views.MutationForces.CopyFromCPU(forces);
            bookkeeping.Views.CrossoverProbabilities.CopyFromCPU(probabilities);
            var before = new double[count];
            var after = new double[count];
            before[1] = double.PositiveInfinity;
            after[2] = double.NegativeInfinity;
            // Four independently specified cases. The last success is in the next sum chunk.
            var minNormal = BitConverter.Int64BitsToDouble(1L << 52);
            (double FirstParent, double LastParent, double LastTrial, double F, double Cr)[] cases =
            [
                (1, 3, 0, 0.7, lShade ? 0.5 : 0.375),
                (double.MaxValue, double.MaxValue, -double.MaxValue, 19.0 / 28, lShade ? 11.0 / 20 : 5.0 / 12),
                (double.Epsilon, double.Epsilon, -2 * double.Epsilon, 0.7, lShade ? 0.5 : 0.375),
                (double.Epsilon, BitConverter.Int64BitsToDouble((1L << 52) + 2), minNormal, 19.0 / 28, lShade ? 11.0 / 20 : 5.0 / 12),
            ];
            foreach (var (firstParent, lastParent, lastTrial, expectedF, expectedCr) in cases)
            {
                before[0] = firstParent;
                before[^1] = lastParent;
                after[^1] = lastTrial;
                parents.View.CopyFromCPU(before);
                survivors.View.CopyFromCPU(after);
                bookkeeping.AfterGeneration(ref views, 1, count, 0, count, 0);
                var memory = new double[2];
                bookkeeping.Views.Adaptation.CopyToCPU(memory);
                Assert.Equal(expectedCr, memory[0]);
                Assert.Equal(expectedF, memory[1]);
            }

            // Cross-chunk products, including a maximum parameter on the smaller weight.
            var factor = Math.ScaleB(1, -54);
            var small = Math.ScaleB(1, -600);
            var tiny = Math.ScaleB(1, -1000);
            (double W1, double W2, double X1, double X2, double Lehmer, double Arithmetic)[] products =
            [
                (Math.ScaleB(1, -969), Math.ScaleB(1, -968), factor, factor, factor, factor),
                (1, 1, small, small, small, small),
                (1, tiny, tiny, 1, 0.5, 2 * tiny),
                (Math.ScaleB(1, 1000), Math.ScaleB(1, -50), Math.ScaleB(1, -1050), 1, 0.5, Math.ScaleB(1, -1049)),
                (tiny, 2 * tiny, tiny, tiny, tiny, tiny),
                (1, 1, double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon),
            ];
            after[^1] = 0;
            survivors.View.CopyFromCPU(after);
            foreach (var (w1, w2, x1, x2, lehmer, arithmetic) in products)
            {
                before[0] = w1;
                before[^1] = w2;
                parents.View.CopyFromCPU(before);
                forces[0] = probabilities[0] = x1;
                forces[^1] = probabilities[^1] = x2;
                bookkeeping.Views.MutationForces.CopyFromCPU(forces);
                bookkeeping.Views.CrossoverProbabilities.CopyFromCPU(probabilities);
                bookkeeping.AfterGeneration(ref views, 1, count, 0, count, 0);
                var memory = new double[2];
                bookkeeping.Views.Adaptation.CopyToCPU(memory);
                Assert.Equal(lehmer, memory[1]);
                Assert.Equal(lShade ? lehmer : arithmetic, memory[0]);
            }

            // When only the second chunk has a positive CR, even one subnormal unit
            // keeps L-SHADE nonterminal. SHADE rounds half of epsilon to even (+0).
            before[0] = before[^1] = 1;
            parents.View.CopyFromCPU(before);
            forces[0] = forces[^1] = 0.75;
            bookkeeping.Views.MutationForces.CopyFromCPU(forces);
            probabilities[0] = -0.0;
            foreach (var lastCr in new[] { double.Epsilon, -0.0 })
            {
                probabilities[^1] = lastCr;
                bookkeeping.Views.CrossoverProbabilities.CopyFromCPU(probabilities);
                bookkeeping.AfterGeneration(ref views, 1, count, 0, count, 0);
                var memory = new double[2];
                bookkeeping.Views.Adaptation.CopyToCPU(memory);
                var expected = lShade ? (lastCr == 0 ? AdaptationRules.TerminalCr : double.Epsilon) : 0.0;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(memory[0]));
                Assert.Equal(0.75, memory[1]);
            }
        }
    }
}
