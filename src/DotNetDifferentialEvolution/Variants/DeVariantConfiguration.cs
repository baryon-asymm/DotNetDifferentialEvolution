namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// The problem dimensions a variant is configured against: everything the builder already knows
/// by the time a variant is chosen, and everything a variant legitimately needs to size its
/// strategies (SHADE's <c>p ∈ [2/N, 0.2]</c> and L-SHADE's archive both scale with the
/// population, and the selection strategy is sized by the genome).
/// </summary>
/// <param name="PopulationSize">The configured population size — the <em>initial</em> size for a
/// variant that reduces it.</param>
/// <param name="GenomeSize">The number of genes per individual.</param>
/// <param name="LowerBound">The lower bound of each gene.</param>
/// <param name="UpperBound">The upper bound of each gene.</param>
public readonly record struct DeVariantConfiguration(
    int PopulationSize,
    int GenomeSize,
    ReadOnlyMemory<double> LowerBound,
    ReadOnlyMemory<double> UpperBound);
