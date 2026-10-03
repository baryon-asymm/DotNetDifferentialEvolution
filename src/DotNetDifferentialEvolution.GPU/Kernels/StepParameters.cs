namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The scalar parameters of a launch, passed by value to every thread.
/// </summary>
/// <param name="Seed">The run's seed, the Philox key.</param>
/// <param name="Generation">The generation the launch builds; 0 for the initial sampling.</param>
/// <param name="PopulationSize">N, the number of individuals.</param>
/// <param name="GenomeSize">D, the number of genes per individual.</param>
/// <param name="MutationForce">F.</param>
/// <param name="CrossoverThreshold">CR scaled to 64 bits (<see cref="DeStep.CrossoverThreshold"/>).</param>
internal readonly record struct StepParameters(
    int Seed,
    int Generation,
    int PopulationSize,
    int GenomeSize,
    double MutationForce,
    ulong CrossoverThreshold);
