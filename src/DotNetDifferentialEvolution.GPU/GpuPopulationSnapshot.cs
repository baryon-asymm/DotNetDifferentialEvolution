namespace DotNetDifferentialEvolution.GPU;

/// <summary>A host copy of the population after a generation, owned by whoever receives it.</summary>
public sealed class GpuPopulationSnapshot
{
    internal GpuPopulationSnapshot(int generation, long evaluationCount, int populationSize, int genomeSize, double[] genes, double[] fitnessFunctionValues)
    {
        Generation = generation;
        EvaluationCount = evaluationCount;
        PopulationSize = populationSize;
        GenomeSize = genomeSize;
        Genes = genes;
        FitnessFunctionValues = fitnessFunctionValues;
    }

    /// <summary>Gets the number of generations run so far.</summary>
    public int Generation { get; }

    /// <summary>Gets the number of evaluations so far, starting at N for the initial population.</summary>
    public long EvaluationCount { get; }

    /// <summary>Gets N, the number of individuals.</summary>
    public int PopulationSize { get; }

    /// <summary>Gets D, the number of genes per individual.</summary>
    public int GenomeSize { get; }

    /// <summary>Gets the genes, individual-major: individual i is <c>[i·D, (i+1)·D)</c>.</summary>
    public ReadOnlyMemory<double> Genes { get; }

    /// <summary>Gets the fitness value of each individual.</summary>
    public ReadOnlyMemory<double> FitnessFunctionValues { get; }
}
