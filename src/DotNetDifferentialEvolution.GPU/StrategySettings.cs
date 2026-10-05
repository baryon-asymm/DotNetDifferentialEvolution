using DotNetDifferentialEvolution.GPU.Kernels;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The scheme and the parameter rule a builder method chose, with the CPU package's settings for it (API.md, symmetry):
/// the fixed schemes accept ties and keep no archive; jDE is rand/1 with per-individual F and CR; JADE, SHADE and L-SHADE
/// are current-to-pbest/1 with an archive, and JADE alone refuses ties.
/// </summary>
/// <param name="Name">The builder method, for messages.</param>
/// <param name="Scheme">The mutation scheme.</param>
/// <param name="Rule">The parameter rule.</param>
/// <param name="MutationForce">F of the fixed schemes; jDE's initial F.</param>
/// <param name="CrossoverProbability">CR of the fixed schemes; jDE's initial CR.</param>
/// <param name="AcceptsTies">Whether an equal trial replaces its parent.</param>
/// <param name="PBestRate">p of current-to-pbest.</param>
/// <param name="ArchiveSizeRate">The archive's capacity per individual; 0 for none.</param>
/// <param name="MemorySize">SHADE's H.</param>
/// <param name="AdaptationRate">JADE's c.</param>
/// <param name="LShadeBudget">L-SHADE's evaluation budget, or <see langword="null"/> for every other configuration.</param>
internal sealed record StrategySettings(
    string Name,
    SchemeKind Scheme,
    ParameterRule Rule,
    double MutationForce,
    double CrossoverProbability,
    bool AcceptsTies,
    double PBestRate,
    double ArchiveSizeRate,
    int MemorySize,
    double AdaptationRate,
    long? LShadeBudget)
{
    /// <summary>A fixed-F scheme: F and CR for every trial, ties accepted.</summary>
    /// <param name="name">The builder method.</param>
    /// <param name="scheme">The scheme.</param>
    /// <param name="mutationForce">F.</param>
    /// <param name="crossoverProbability">CR.</param>
    /// <returns>The settings.</returns>
    public static StrategySettings Fixed(string name, SchemeKind scheme, double mutationForce, double crossoverProbability) =>
        new(name, scheme, ParameterRule.Fixed, mutationForce, crossoverProbability, true, 0.0, 0.0, 0, 0.0, null);

    /// <summary>Gets the smallest population the scheme can draw its donors from; L-SHADE's own minimum is the same 4.</summary>
    public int MinimumPopulationSize => Schemes.MinimumPopulationSize(Scheme);

    /// <summary>The smallest p of current-to-pbest for <paramref name="populationSize"/>: SHADE's <c>min(2/N, p)</c>, else p.</summary>
    /// <param name="populationSize">N.</param>
    /// <returns>The smallest p.</returns>
    public double PBestRateMin(int populationSize) =>
        Rule == ParameterRule.Shade && LShadeBudget is null ? Math.Min(2.0 / populationSize, PBestRate) : PBestRate;
}
