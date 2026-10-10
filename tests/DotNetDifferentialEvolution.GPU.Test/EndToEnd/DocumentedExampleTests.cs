using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>
/// The quick start of the package's <c>README.md</c>, compiled and run. Keep the body textually
/// identical to the README's snippet: change both in the same commit. The differences are the ones
/// the test project imposes and a caller is not bound by: <c>.ConfigureAwait(true)</c> after the
/// <c>await</c> (CA2007, xUnit1030), the objective struct <c>internal</c> rather than
/// <c>public</c> (CA1515; ILGPU sees it through <c>InternalsVisibleTo("ILGPURuntime")</c>), and
/// the two <c>Console.WriteLine</c> lines replaced by assertions on the same values. The README's code runs
/// as it is, under <c>Gpu</c>, on whatever device Auto finds (an N = 10 000 run: on a machine with CUDA, on CUDA). The
/// same code on the CPU accelerator, which CI runs, is <see cref="DocumentedExampleOnTheCpuTests"/>: no test outside
/// <c>Gpu</c> opens a device (ACCEPTANCE.md of Kernels, check A12).
/// </summary>
public class DocumentedExampleTests
{
    /// <summary>The quick start builds on whatever device Auto finds, runs, and reaches Sphere's minimum.</summary>
    /// <returns>The test's task.</returns>
    [Fact]
    [Trait("Category", "Gpu")]
    public async Task TheQuickStartBuildsRunsAndReachesTheMinimum()
    {
        double[] lowerBound = [-5.0, -5.0, -5.0, -5.0, -5.0];
        double[] upperBound = [5.0, 5.0, 5.0, 5.0, 5.0];

        using var optimizer = GpuDifferentialEvolutionBuilder
            .ForFunction(new Sphere())
            .WithBounds(lowerBound, upperBound)
            .WithPopulationSize(10_000)
            .WithDefaultMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)
            .WithGenerationLimit(500)
            .OnDevice(GpuDevice.Auto)
            .WithSeed(1)
            .Build();

        var result = await optimizer.RunAsync().ConfigureAwait(true);

        Assert.True(result.FitnessFunctionValue < 1e-12, $"{result.Device.Kind} ({result.Device.Name}): f = {result.FitnessFunctionValue}");
        Assert.Equal(5, result.Genes.Length);
    }

    /// <summary>The README's objective, unchanged but for its accessibility.</summary>
    internal readonly struct Sphere : IGpuFitnessFunction
    {
        public double Evaluate(GeneView genes)
        {
            var sum = 0.0;
            for (var j = 0; j < genes.Length; j++)
            {
                sum += genes[j] * genes[j];
            }

            return sum;
        }
    }
}
