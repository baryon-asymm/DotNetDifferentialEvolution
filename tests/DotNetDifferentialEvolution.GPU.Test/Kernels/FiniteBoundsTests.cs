using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using DotNetDifferentialEvolution.GPU.Test.Random;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>Sampling and crossover on the accelerator, before selection can hide an invalid gene.</summary>
public class FiniteBoundsTests
{
    /// <summary>Checks generation-zero samples on the CI accelerator.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SamplesOnCpu() => CheckSamples(GpuDevice.Cpu);

    /// <summary>Checks generation-zero samples on a hardware backend.</summary>
    /// <param name="device">The backend.</param>
    [Theory]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    [Trait("Category", "Gpu")]
    public void SamplesOnDevice(GpuDevice device) => CheckSamples(device);

    /// <summary>Checks repairs on the CI accelerator.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void RepairsOnCpu() => CheckRepairs(GpuDevice.Cpu);

    /// <summary>Checks repairs on a hardware backend.</summary>
    /// <param name="device">The backend.</param>
    [Theory]
    [InlineData(GpuDevice.Cuda)]
    [InlineData(GpuDevice.OpenCL)]
    [Trait("Category", "Gpu")]
    public void RepairsOnDevice(GpuDevice device) => CheckRepairs(device);

    private static void CheckSamples(GpuDevice device)
    {
        const int size = 16;
        const int seed = 71;
        var radius = Math.ScaleB(1.0, 1023);
        double[] lower = [-radius, -3.0, double.MaxValue];
        double[] upper = [radius, 5.0, double.MaxValue];
        using var context = TestDevices.CreateContext(device);
        using var accelerator = TestDevices.CreateAccelerator(context, device);
        using var lo = accelerator.Allocate1D(lower);
        using var hi = accelerator.Allocate1D(upper);
        using var current = accelerator.Allocate1D<double>(size * lower.Length);
        var views = new PopulationViews(current.View, default, default, default, default, lo.View, hi.View);
        var sample = accelerator.LoadAutoGroupedStreamKernel<Index1D, StepParameters, PopulationViews>(PointwiseKernels.Sample);
        sample(size, new StepParameters(seed, 0, size, lower.Length, 0.0, 0UL), views);
        var genes = current.GetAsArray1D();

        for (var i = 0; i < size; i++)
        {
            var draws = new PhiloxDraws(seed, i, 0);
            // The exact interpolation on [-2^1023, 2^1023] is a power-of-two scaling.
            var offset = i * lower.Length;
            Assert.Equal((2.0 * draws.NextUnitDouble() - 1.0) * radius, genes[offset]);
            // Width eight makes the product exact even on a backend that contracts multiply-add.
            Assert.Equal(-3.0 + draws.NextUnitDouble() * 8.0, genes[offset + 1]);
            Assert.Equal(double.MaxValue, genes[offset + 2]);
        }
    }

    private static void CheckRepairs(GpuDevice device)
    {
        // In units of 2^1021, the two midpoints are (7 + 5) / 2 and (-7 - 5) / 2.
        var unit = Math.ScaleB(1.0, 1021);
        double[] lower = [0.0, -7.0 * unit, -1.0, double.Epsilon, -10.0];
        double[] upper = [7.0 * unit, 0.0, 1.0, double.Epsilon, 10.0];
        double[] parents = [5.0 * unit, -5.0 * unit, -0.5, double.Epsilon, 1.0];
        double[] mutants = [double.PositiveInfinity, double.NegativeInfinity, double.NaN, 0.0, 3.0];
        double[] expected = [6.0 * unit, -6.0 * unit, -0.5, double.Epsilon, 3.0];
        using var context = TestDevices.CreateContext(device);
        using var accelerator = TestDevices.CreateAccelerator(context, device);
        using var lo = accelerator.Allocate1D(lower);
        using var hi = accelerator.Allocate1D(upper);
        using var current = accelerator.Allocate1D(parents);
        using var trial = accelerator.Allocate1D(mutants);
        var views = new PopulationViews(current.View, default, default, default, trial.View, lo.View, hi.View);
        var repair = accelerator.LoadAutoGroupedStreamKernel<Index1D, int, PopulationViews>(Repair);
        repair(1, mutants.Length, views);

        ParityCases.AssertSameBits(expected, trial.GetAsArray1D(), device.ToString());
    }

    private static void Repair(Index1D index, int genomeSize, PopulationViews views)
    {
        var draws = new PhiloxDraws(71, index, 1);
        DeStep.CrossAndRepair(ref draws, index, genomeSize, ulong.MaxValue, views.Current, views.Trial, views.LowerBound, views.UpperBound);
    }
}
