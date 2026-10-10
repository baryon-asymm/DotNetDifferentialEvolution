namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// What a test forces on a <see cref="GenerationBookkeeping"/> instead of letting it decide (checks A3 and A4): the
/// ranking limit, with nothing timed when it is given, and the wide chunk. A <see langword="null"/> member is decided as
/// the package decides it.
/// </summary>
/// <param name="RankingLimit">L: counting ranks a population of at most this size; at least 1.</param>
/// <param name="WideChunkSize">c: the individuals one thread of a wide pass walks; at least 1.</param>
internal sealed record BookkeepingTuning(int? RankingLimit = null, int? WideChunkSize = null);
