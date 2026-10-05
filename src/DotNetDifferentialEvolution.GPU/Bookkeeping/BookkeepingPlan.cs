using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>What a configuration keeps on the device between generations.</summary>
/// <param name="PopulationSize">N at the start; the buffers are sized for it.</param>
/// <param name="GenomeSize">D.</param>
/// <param name="Scheme">The mutation scheme.</param>
/// <param name="Rule">The parameter rule.</param>
/// <param name="ArchiveCapacity">The archive's capacity at the start; 0 for none.</param>
/// <param name="MemorySize">SHADE's H; 0 otherwise.</param>
/// <param name="AdaptationRate">JADE's c.</param>
/// <param name="LShade">Whether L-SHADE's terminal CR and Lehmer CR mean apply.</param>
/// <param name="InitialMutationForce">jDE's initial F.</param>
/// <param name="InitialCrossoverProbability">jDE's initial CR.</param>
/// <param name="Stagnation">The stagnation rule's threshold and streak, or <see langword="null"/>.</param>
internal sealed record BookkeepingPlan(
    int PopulationSize,
    int GenomeSize,
    SchemeKind Scheme,
    ParameterRule Rule,
    int ArchiveCapacity,
    int MemorySize,
    double AdaptationRate,
    bool LShade,
    double InitialMutationForce,
    double InitialCrossoverProbability,
    (double Threshold, int MaxStreak)? Stagnation)
{
    /// <summary>Gets a value indicating whether the generation reads the best index, or the stop rule does.</summary>
    public bool NeedsBestIndex => Scheme is SchemeKind.Best or SchemeKind.CurrentToBest or SchemeKind.BestTwo || Stagnation is not null;

    /// <summary>Gets a value indicating whether the generation reads the ranking (current-to-pbest; L-SHADE's reduction).</summary>
    public bool NeedsRanking => Scheme == SchemeKind.CurrentToPBest;

    /// <summary>Gets a value indicating whether JADE's means or SHADE's memory are adapted.</summary>
    public bool Adapts => Rule is ParameterRule.Jade or ParameterRule.Shade;
}
