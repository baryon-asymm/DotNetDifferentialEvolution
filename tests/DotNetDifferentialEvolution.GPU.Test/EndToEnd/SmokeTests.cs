using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.EndToEnd;

/// <summary>Temporary smoke run while the v1 suites are written.</summary>
public class SmokeTests
{
    /// <summary>Sphere 5-D on the CPU accelerator.</summary>
    /// <returns>The test's task.</returns>
    /// <param name="device">The device.</param>
    [Theory]
    [InlineData(GpuDevice.Cpu)]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    public async Task SphereRunsOnTheCpuAccelerator(GpuDevice device)
    {
        double[] lower = [-5.0, -5.0, -5.0, -5.0, -5.0];
        double[] upper = [5.0, 5.0, 5.0, 5.0, 5.0];
        using var optimizer = GpuDifferentialEvolutionBuilder
            .ForFunction(default(Sphere))
            .WithBounds(lower, upper)
            .WithPopulationSize(50)
            .WithDefaultMutationStrategy(0.5, 0.9)
            .WithGenerationLimit(300)
            .OnDevice(device)
            .WithSeed(1)
            .Build();
        var result = await optimizer.RunAsync().ConfigureAwait(true);
        Assert.True(result.FitnessFunctionValue < 1e-6, $"f = {result.FitnessFunctionValue}");
        Assert.Equal(300, result.Generations);
        Assert.Equal(50L * 301, result.EvaluationCount);
        Console.WriteLine(device + " " + result.Device + " f=" + result.FitnessFunctionValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

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
