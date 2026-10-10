namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>What the constructor's calibration measured at one population size: for the <b>Gpu</b> test that prints it (check A3).</summary>
/// <param name="Count">The population size n the rankings were timed at.</param>
/// <param name="Times">The two rankings' median time at n, in microseconds.</param>
internal readonly record struct RankingMeasurement(int Count, RankingTimes Times);
