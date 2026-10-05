using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The device state a generation reads besides the population, and the records it writes for the work between
/// generations. A configuration that does not use a view passes a one-element buffer for it.
/// </summary>
/// <param name="Stop">The stop word: entry 0 is non-zero once the stagnation rule has fired, and every kernel returns at once.</param>
/// <param name="BestIndex">Entry 0: the index of the best individual of the current population.</param>
/// <param name="Ranking">The current population's indices, best first.</param>
/// <param name="Archive">The archive, individual-major, capacity·D genes.</param>
/// <param name="ArchiveSize">Entry 0: the archive's size.</param>
/// <param name="Adaptation">JADE: μCR, μF. SHADE: the H CR slots, then the H F slots.</param>
/// <param name="MutationForces">jDE: the F of each individual. JADE, SHADE: the F each trial used.</param>
/// <param name="CrossoverProbabilities">jDE: the CR of each individual. JADE, SHADE: the CR each trial used.</param>
/// <param name="Outcomes">JADE, SHADE: each trial's outcome (<see cref="Selection"/>).</param>
internal readonly record struct StrategyViews(
    ArrayView<int> Stop,
    ArrayView<int> BestIndex,
    ArrayView<int> Ranking,
    ArrayView<double> Archive,
    ArrayView<int> ArchiveSize,
    ArrayView<double> Adaptation,
    ArrayView<double> MutationForces,
    ArrayView<double> CrossoverProbabilities,
    ArrayView<int> Outcomes);
