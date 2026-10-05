namespace DotNetDifferentialEvolution.GPU;

/// <summary>Everything the builder collected, validated, for one optimizer.</summary>
/// <param name="LowerBound">The lower bound of each gene.</param>
/// <param name="UpperBound">The upper bound of each gene.</param>
/// <param name="PopulationSize">N; under L-SHADE the initial size.</param>
/// <param name="Strategy">The scheme and the parameter rule.</param>
/// <param name="MaxGenerations">The generation limit, or <see langword="null"/>.</param>
/// <param name="MaxEvaluations">The evaluation limit, or <see langword="null"/>.</param>
/// <param name="Stagnation">The stagnation limit, or <see langword="null"/>.</param>
/// <param name="Seed">The seed.</param>
/// <param name="Handler">The observer, or <see langword="null"/>.</param>
/// <param name="EveryNGenerations">The observer's period.</param>
internal sealed record RunSettings(
    double[] LowerBound,
    double[] UpperBound,
    int PopulationSize,
    StrategySettings Strategy,
    int? MaxGenerations,
    long? MaxEvaluations,
    (double Threshold, int MaxStreak)? Stagnation,
    int Seed,
    IGpuPopulationUpdatedHandler? Handler,
    int EveryNGenerations)
{
    /// <summary>The generations between two reads of the stop word under a stagnation limit (BOOT.md, invariant 5).</summary>
    public const int DefaultStopReadInterval = 16;

    /// <summary>Gets D, the number of genes per individual.</summary>
    public int GenomeSize => LowerBound.Length;

    /// <summary>Gets the generations between two reads of the stop word; the tests also read it every generation (ACCEPTANCE.md, S12).</summary>
    public int StopReadInterval { get; init; } = DefaultStopReadInterval;

    /// <summary>Whether the run stops after <paramref name="generations"/> generations and <paramref name="evaluations"/> evaluations.</summary>
    /// <param name="generations">The generations run so far.</param>
    /// <param name="evaluations">The evaluations so far.</param>
    /// <returns><see langword="true"/> when a limit is reached.</returns>
    public bool LimitReached(int generations, long evaluations) =>
        (MaxGenerations is { } maxGenerations && generations >= maxGenerations)
        || (MaxEvaluations is { } maxEvaluations && evaluations >= maxEvaluations);
}
