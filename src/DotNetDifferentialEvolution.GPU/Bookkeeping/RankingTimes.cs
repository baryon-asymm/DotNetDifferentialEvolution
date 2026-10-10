namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>The two rankings' time at one population size, in microseconds: what the calibration compares (check A3).</summary>
/// <param name="Counting">The time of one ranking by counting.</param>
/// <param name="Bitonic">The time of one ranking by the bitonic network.</param>
internal readonly record struct RankingTimes(double Counting, double Bitonic);
