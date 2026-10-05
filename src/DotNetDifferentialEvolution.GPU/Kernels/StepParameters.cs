namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The scalar parameters of a launch, passed by value to every thread.
/// </summary>
/// <param name="Seed">The run's seed, the Philox key.</param>
/// <param name="Generation">The generation the launch builds; 0 for the initial sampling.</param>
/// <param name="PopulationSize">N, the number of individuals; under L-SHADE the current size.</param>
/// <param name="GenomeSize">D, the number of genes per individual.</param>
/// <param name="MutationForce">F of <see cref="ParameterRule.Fixed"/>.</param>
/// <param name="CrossoverThreshold">CR of <see cref="ParameterRule.Fixed"/>, scaled to 64 bits (<see cref="DeStep.CrossoverThreshold"/>).</param>
/// <param name="Scheme">The mutation scheme.</param>
/// <param name="Rule">Where F and CR come from.</param>
/// <param name="Ties">Whether a trial as good as its parent replaces it; JADE refuses ties.</param>
/// <param name="PBestRateMin">The smallest p of current-to-pbest.</param>
/// <param name="PBestRateMax">The largest p of current-to-pbest; p is drawn from [min, max] when they differ.</param>
/// <param name="MemorySize">H, the number of SHADE memory slots.</param>
internal readonly record struct StepParameters(
    int Seed,
    int Generation,
    int PopulationSize,
    int GenomeSize,
    double MutationForce,
    ulong CrossoverThreshold,
    SchemeKind Scheme = SchemeKind.RandOne,
    ParameterRule Rule = ParameterRule.Fixed,
    TieRule Ties = TieRule.Accepted,
    double PBestRateMin = 0.0,
    double PBestRateMax = 0.0,
    int MemorySize = 0);
