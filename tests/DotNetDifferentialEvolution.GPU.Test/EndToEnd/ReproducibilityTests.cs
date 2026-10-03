namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check 4a of the GPU package's ACCEPTANCE.md, reproducibility. The same seed, run twice on the
/// same device, gives a bit-identical final population (every gene and every fitness value,
/// compared as IEEE 754 bit patterns) and the same result; seeds 1 and 2 give different
/// populations. On the CPU accelerator in CI, and on CUDA and OpenCL under <c>Gpu</c>; results
/// are never compared across devices (the package's BOOT.md, invariant 4).
/// </summary>
public class ReproducibilityTests
{
    private const int Generations = 40;

    private static readonly double[] Lower = [-5.12, -5.12, -5.12];
    private static readonly double[] Upper = [5.12, 5.12, 5.12];

    /// <summary>Seed 1 twice on the CPU accelerator gives bit-identical genes and fitness.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task TheSameSeedTwiceIsBitIdenticalOnTheCpuAccelerator() => AssertTheSameSeedIsBitIdentical(GpuDevice.Cpu);

    /// <summary>Seeds 1 and 2 on the CPU accelerator give different populations.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task SeedsOneAndTwoDifferOnTheCpuAccelerator() => AssertSeedsOneAndTwoDiffer(GpuDevice.Cpu);

    /// <summary>Seed 1 twice on each GPU gives bit-identical genes and fitness.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public Task TheSameSeedTwiceIsBitIdenticalOnTheGpu(GpuDevice device) => AssertTheSameSeedIsBitIdentical(device);

    /// <summary>Seeds 1 and 2 on each GPU give different populations.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public Task SeedsOneAndTwoDifferOnTheGpu(GpuDevice device) => AssertSeedsOneAndTwoDiffer(device);

    private static async Task AssertTheSameSeedIsBitIdentical(GpuDevice device)
    {
        var first = await RunAsync(1, device).ConfigureAwait(true);
        var second = await RunAsync(1, device).ConfigureAwait(true);

        Assert.Equal(first.Genes, second.Genes);
        Assert.Equal(first.Fitness, second.Fitness);
        Assert.Equal(first.BestGenes, second.BestGenes);
        Assert.Equal(first.BestFitness, second.BestFitness);
    }

    private static async Task AssertSeedsOneAndTwoDiffer(GpuDevice device)
    {
        var one = await RunAsync(1, device).ConfigureAwait(true);
        var two = await RunAsync(2, device).ConfigureAwait(true);

        Assert.NotEqual(one.Genes, two.Genes);
        Assert.NotEqual(one.Fitness, two.Fitness);
    }

    private static long[] Bits(ReadOnlySpan<double> values)
    {
        var bits = new long[values.Length];
        for (var k = 0; k < values.Length; k++)
        {
            bits[k] = BitConverter.DoubleToInt64Bits(values[k]);
        }

        return bits;
    }

    private static async Task<RunBits> RunAsync(int seed, GpuDevice device)
    {
        // The observer is due once, at the last generation: it hands over the final population.
        var observer = new RecordingObserver();
        using var optimizer = GpuDifferentialEvolutionBuilder.ForFunction(default(Rastrigin))
            .WithBounds(Lower, Upper)
            .WithPopulationSize(32)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(Generations)
            .OnDevice(device)
            .WithSeed(seed)
            .WithPopulationUpdateHandler(observer, Generations)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        var last = Assert.IsType<GpuPopulationSnapshot>(observer.Last);
        Assert.Equal(Generations, last.Generation);
        return new RunBits(
            Bits(last.Genes.Span),
            Bits(last.FitnessFunctionValues.Span),
            Bits(result.Genes.Span),
            BitConverter.DoubleToInt64Bits(result.FitnessFunctionValue));
    }

    private sealed record RunBits(long[] Genes, long[] Fitness, long[] BestGenes, long BestFitness);
}
