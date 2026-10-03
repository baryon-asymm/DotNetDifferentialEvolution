namespace DotNetDifferentialEvolution.GPU;

/// <summary>Everything the builder collected, validated, for one optimizer.</summary>
/// <param name="LowerBound">The lower bound of each gene.</param>
/// <param name="UpperBound">The upper bound of each gene.</param>
/// <param name="PopulationSize">N.</param>
/// <param name="MutationForce">F.</param>
/// <param name="CrossoverProbability">CR.</param>
/// <param name="MaxGenerations">The generation limit, or <see langword="null"/>.</param>
/// <param name="MaxEvaluations">The evaluation limit, or <see langword="null"/>.</param>
/// <param name="Seed">The seed.</param>
/// <param name="Handler">The observer, or <see langword="null"/>.</param>
/// <param name="EveryNGenerations">The observer's period.</param>
internal sealed record RunSettings(
    double[] LowerBound,
    double[] UpperBound,
    int PopulationSize,
    double MutationForce,
    double CrossoverProbability,
    int? MaxGenerations,
    long? MaxEvaluations,
    int Seed,
    IGpuPopulationUpdatedHandler? Handler,
    int EveryNGenerations)
{
    /// <summary>Gets D, the number of genes per individual.</summary>
    public int GenomeSize => LowerBound.Length;

    /// <summary>Whether the run stops after <paramref name="generations"/> generations and <paramref name="evaluations"/> evaluations.</summary>
    /// <param name="generations">The generations run so far.</param>
    /// <param name="evaluations">The evaluations so far.</param>
    /// <returns><see langword="true"/> when a limit is reached.</returns>
    public bool LimitReached(int generations, long evaluations) =>
        (MaxGenerations is { } maxGenerations && generations >= maxGenerations)
        || (MaxEvaluations is { } maxEvaluations && evaluations >= maxEvaluations);
}
