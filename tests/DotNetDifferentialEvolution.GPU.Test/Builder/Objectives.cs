using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU.Test.Builder;

/// <summary>Σ x_j², the objective the builder cases build with.</summary>
internal readonly struct Sphere : IGpuFitnessFunction
{
    /// <inheritdoc />
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

/// <summary>
/// An objective ILGPU cannot compile: its body contains a <c>throw</c>. The branch is never taken
/// at run time; the instruction alone stops the kernel compiler, on the CPU accelerator as on
/// CUDA and OpenCL (measured 2026-10-03).
/// </summary>
internal readonly struct Throwing : IGpuFitnessFunction
{
    /// <inheritdoc />
    public double Evaluate(GeneView genes)
    {
        return double.IsNaN(genes[0])
            ? throw new InvalidOperationException("Never reached: the kernel cannot be compiled.")
            : genes[0];
    }
}

/// <summary>An observer that ignores what it gets; the builder cases only pass it in.</summary>
internal sealed class IgnoringHandler : IGpuPopulationUpdatedHandler
{
    /// <inheritdoc />
    public void Handle(GpuPopulationSnapshot snapshot)
    {
    }
}
