namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// The quick start of the package's <c>README.md</c> with <c>GpuDevice.Cpu</c> in place of <c>GpuDevice.Auto</c>, the only
/// difference from <see cref="DocumentedExampleTests"/>, which runs it on Auto under <c>Gpu</c>: the same N = 10 000 run on
/// the CPU accelerator, so that CI compiles and runs the example without opening a device (check A12). Keep the body
/// textually identical to that test's, device apart.
/// </summary>
[Trait("Category", "Integration")]
public class DocumentedExampleOnTheCpuTests
{
    /// <summary>The quick start builds on the CPU accelerator, runs, and reaches Sphere's minimum.</summary>
    /// <returns>The test's task.</returns>
    [Fact]
    public async Task TheQuickStartBuildsRunsAndReachesTheMinimum()
    {
        double[] lowerBound = [-5.0, -5.0, -5.0, -5.0, -5.0];
        double[] upperBound = [5.0, 5.0, 5.0, 5.0, 5.0];

        using var optimizer = GpuDifferentialEvolutionBuilder
            .ForFunction(new DocumentedExampleTests.Sphere())
            .WithBounds(lowerBound, upperBound)
            .WithPopulationSize(10_000)
            .WithDefaultMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)
            .WithGenerationLimit(500)
            .OnDevice(GpuDevice.Cpu)
            .WithSeed(1)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        Assert.True(result.FitnessFunctionValue < 1e-12, $"{result.Device.Kind} ({result.Device.Name}): f = {result.FitnessFunctionValue}");
        Assert.Equal(5, result.Genes.Length);
    }
}
