using DotNetOptimization.Abstractions;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The outcome of a run: the best individual of the final population (<see cref="double.NaN"/> ranks
/// worst; a tie goes to the lowest index), with the run's counters and its device.
/// </summary>
public sealed class GpuOptimizationResult : ISolution
{
    internal GpuOptimizationResult(double[] genes, double fitnessFunctionValue, int generations, long evaluationCount, GpuDeviceInfo device)
    {
        Genes = genes;
        FitnessFunctionValue = fitnessFunctionValue;
        Generations = generations;
        EvaluationCount = evaluationCount;
        Device = device;
    }

    /// <summary>Gets the best individual's genes.</summary>
    public ReadOnlyMemory<double> Genes { get; }

    /// <summary>Gets the best individual's fitness value.</summary>
    public double FitnessFunctionValue { get; }

    /// <summary>Gets the number of generations run.</summary>
    public int Generations { get; }

    /// <summary>Gets the number of evaluations, the initial N included.</summary>
    public long EvaluationCount { get; }

    /// <summary>Gets the device the run was on.</summary>
    public GpuDeviceInfo Device { get; }
}
