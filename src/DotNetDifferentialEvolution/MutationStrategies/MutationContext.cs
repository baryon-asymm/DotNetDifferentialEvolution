using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// Carries everything a mutation strategy needs to build one trial vector. It is a
/// <see langword="ref struct"/> so the population spans are passed without copying.
/// </summary>
public readonly ref struct MutationContext
{
    /// <summary>The index of the individual being mutated (the target / current vector).</summary>
    public int IndividualIndex { get; init; }

    /// <summary>The index of the best individual in the current population.</summary>
    public int BestIndividualIndex { get; init; }

    /// <summary>The number of active individuals in the population for this generation.</summary>
    public int PopulationSize { get; init; }

    /// <summary>The number of genes per individual.</summary>
    public int GenomeSize { get; init; }

    /// <summary>The mutation factor (F) to use for this trial.</summary>
    public double MutationForce { get; init; }

    /// <summary>The crossover probability (CR) to use for this trial.</summary>
    public double CrossoverProbability { get; init; }

    /// <summary>The current population genes (flattened, length <c>PopulationSize * GenomeSize</c>).</summary>
    public ReadOnlySpan<double> Population { get; init; }

    /// <summary>The fitness function values of the current population.</summary>
    public ReadOnlySpan<double> PopulationFfValues { get; init; }

    /// <summary>The destination buffer for the produced trial individual (length <c>GenomeSize</c>).</summary>
    public Span<double> TrialIndividual { get; init; }

    /// <summary>The lower bound of each gene.</summary>
    public ReadOnlySpan<double> LowerBound { get; init; }

    /// <summary>The upper bound of each gene.</summary>
    public ReadOnlySpan<double> UpperBound { get; init; }

    /// <summary>The random provider for this worker.</summary>
    public BaseRandomProvider RandomProvider { get; init; }

    /// <summary>
    /// Optional external archive of previously discarded individuals (flattened genes),
    /// used by JADE/SHADE-style strategies. Empty when no archive is configured.
    /// </summary>
    public ReadOnlySpan<double> Archive { get; init; }

    /// <summary>The number of valid individuals currently stored in <see cref="Archive"/>.</summary>
    public int ArchiveSize { get; init; }

    /// <summary>
    /// Optional population indices sorted ascending by fitness (best first), used by
    /// p-best style strategies. Empty when not provided.
    /// </summary>
    public ReadOnlySpan<int> FitnessSortedIndices { get; init; }
}
