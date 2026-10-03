using DotNetDifferentialEvolution.GPU.Objectives;
using Xunit.Abstractions;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// Check 1h of the GPU package's ACCEPTANCE.md, convergence to a known optimum (the positive
/// control). Seed 1, DE/rand/1/bin with F = 0.5, CR = 0.9, N = 50 and a limit of 1000 generations
/// (chosen 2026-10-03, before the first run, and not tuned since): Sphere 5-D in [−5, 5] reaches
/// 1e-6; Rosenbrock 2-D in [−5, 5] reaches 1e-6 with both genes within 1e-3 of 1; Rastrigin 2-D in
/// [−5.12, 5.12] reaches 1e-4. The optima are analytic. On the CPU accelerator in CI, and again on
/// CUDA and OpenCL under <c>Gpu</c>.
/// </summary>
/// <param name="output">Receives the value and genes each run reached.</param>
public class ConvergenceTests(ITestOutputHelper output)
{
    private const int Seed = 1;
    private const int PopulationSize = 50;
    private const int Generations = 1000;
    private const double MutationForce = 0.5;
    private const double CrossoverProbability = 0.9;

    /// <summary>Sphere 5-D reaches 1e-6 on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task SphereReachesItsMinimumOnTheCpuAccelerator() => AssertSphere(GpuDevice.Cpu);

    /// <summary>Rosenbrock 2-D reaches 1e-6, with the genes within 1e-3 of (1, 1), on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task RosenbrockReachesItsMinimumOnTheCpuAccelerator() => AssertRosenbrock(GpuDevice.Cpu);

    /// <summary>Rastrigin 2-D reaches 1e-4 on the CPU accelerator.</summary>
    /// <returns>The case.</returns>
    [Fact]
    public Task RastriginReachesItsMinimumOnTheCpuAccelerator() => AssertRastrigin(GpuDevice.Cpu);

    /// <summary>Sphere 5-D reaches 1e-6 on each GPU.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public Task SphereReachesItsMinimumOnTheGpu(GpuDevice device) => AssertSphere(device);

    /// <summary>Rosenbrock 2-D reaches 1e-6, with the genes within 1e-3 of (1, 1), on each GPU.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public Task RosenbrockReachesItsMinimumOnTheGpu(GpuDevice device) => AssertRosenbrock(device);

    /// <summary>Rastrigin 2-D reaches 1e-4 on each GPU.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The case.</returns>
    [Theory]
    [Trait("Category", "Gpu")]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public Task RastriginReachesItsMinimumOnTheGpu(GpuDevice device) => AssertRastrigin(device);

    private static double[] Filled(int length, double value) => [.. Enumerable.Repeat(value, length)];

    private async Task AssertSphere(GpuDevice device)
    {
        var result = await RunAsync(default(Sphere), Filled(5, -5.0), Filled(5, 5.0), device).ConfigureAwait(true);

        Assert.True(result.FitnessFunctionValue <= 1e-6, $"Sphere 5-D reached {result.FitnessFunctionValue:R}, not 1e-6.");
    }

    private async Task AssertRosenbrock(GpuDevice device)
    {
        var result = await RunAsync(default(Rosenbrock), Filled(2, -5.0), Filled(2, 5.0), device).ConfigureAwait(true);

        Assert.True(result.FitnessFunctionValue <= 1e-6, $"Rosenbrock 2-D reached {result.FitnessFunctionValue:R}, not 1e-6.");
        var genes = result.Genes.Span;
        for (var j = 0; j < genes.Length; j++)
        {
            Assert.True(Math.Abs(genes[j] - 1.0) <= 1e-3, $"Rosenbrock gene {j} is {genes[j]:R}, not within 1e-3 of 1.");
        }
    }

    private async Task AssertRastrigin(GpuDevice device)
    {
        var result = await RunAsync(default(Rastrigin), Filled(2, -5.12), Filled(2, 5.12), device).ConfigureAwait(true);

        Assert.True(result.FitnessFunctionValue <= 1e-4, $"Rastrigin 2-D reached {result.FitnessFunctionValue:R}, not 1e-4.");
    }

    private async Task<GpuOptimizationResult> RunAsync<TFunction>(TFunction function, double[] lower, double[] upper, GpuDevice device)
        where TFunction : struct, IGpuFitnessFunction
    {
        using var optimizer = GpuDifferentialEvolutionBuilder.ForFunction(function)
            .WithBounds(lower, upper)
            .WithPopulationSize(PopulationSize)
            .WithDefaultMutationStrategy(MutationForce, CrossoverProbability)
            .WithGenerationLimit(Generations)
            .OnDevice(device)
            .WithSeed(Seed)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        output.WriteLine(
            $"{typeof(TFunction).Name} on {result.Device.Name}: f = {result.FitnessFunctionValue:R} at ({string.Join(", ", result.Genes.ToArray().Select(g => g.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}) after {result.Generations} generations");
        Assert.Equal(Generations, result.Generations);
        return result;
    }
}
