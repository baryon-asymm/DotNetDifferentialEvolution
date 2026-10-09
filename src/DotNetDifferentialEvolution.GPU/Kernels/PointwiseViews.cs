using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The device memory the pointwise kernels use besides <see cref="PopulationViews"/> and <see cref="StrategyViews"/>.
/// Point results are individual-major: individual <c>i</c>'s are <c>[i·P, (i+1)·P)</c>.
/// </summary>
/// <typeparam name="TPoint">The result of one point.</typeparam>
/// <param name="Results">The point results, <c>N·P</c>; thread <c>k</c> of the point kernel writes result <c>k</c> only.</param>
/// <param name="PointCount"><c>P</c>, the number of points of an individual.</param>
/// <param name="TrialMutationForces">The F each trial was built with, <c>N</c>; thread <c>i</c> of the build kernel writes entry <c>i</c> only.</param>
/// <param name="TrialCrossoverProbabilities">The CR each trial was built with, <c>N</c>; written like <paramref name="TrialMutationForces"/>.</param>
internal readonly record struct PointwiseViews<TPoint>(
    ArrayView<TPoint> Results,
    int PointCount,
    ArrayView<double> TrialMutationForces,
    ArrayView<double> TrialCrossoverProbabilities)
    where TPoint : unmanaged;
