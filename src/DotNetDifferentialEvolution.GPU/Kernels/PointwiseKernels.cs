using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The kernels of the pointwise path, in which the evaluation leaves the generation kernel: a generation is
/// <see cref="BuildTrials{TRule}"/>, <see cref="EvaluatePoints{TFunction, TPoint}"/>, <see cref="Select{TFunction, TPoint}"/>;
/// the initialisation is <see cref="Sample"/>, <see cref="EvaluatePoints{TFunction, TPoint}"/>,
/// <see cref="CombineInitial{TFunction, TPoint}"/>. The sampling and the selection are the single-kernel path's own
/// functions (<see cref="GpuKernels.SampleIndividual"/>, <see cref="GpuKernels.SelectAndRecord"/>), and the draws are those
/// of <see cref="GpuKernels.Generation{TFunction, TRule}"/>, in the same order.
/// </summary>
internal static class PointwiseKernels
{
    /// <summary>Samples individual <paramref name="index"/> into the current population: <see cref="GpuKernels.SampleIndividual"/>.</summary>
    /// <param name="index">The individual.</param>
    /// <param name="parameters">The seed and D.</param>
    /// <param name="views">The device memory; writes the individual's genes in <c>Current</c>.</param>
    public static void Sample(Index1D index, StepParameters parameters, PopulationViews views)
    {
        int individual = index;
        GpuKernels.SampleIndividual(individual, parameters, views);
    }

    /// <summary>
    /// Thread <c>k</c> evaluates point <c>k mod P</c> of individual <c>k div P</c> of <paramref name="population"/> and writes
    /// the result into entry <c>k</c> of the point results. Launched with <c>N·P</c> threads. Does nothing once the stop word
    /// is set.
    /// </summary>
    /// <typeparam name="TFunction">The objective.</typeparam>
    /// <typeparam name="TPoint">The result of one point.</typeparam>
    /// <param name="index">The thread <c>k</c>.</param>
    /// <param name="function">The objective.</param>
    /// <param name="parameters">D.</param>
    /// <param name="population">The genes to evaluate: the current population at initialisation, the trials in a generation.</param>
    /// <param name="points">The device memory; writes entry <c>k</c> of <c>Results</c>.</param>
    /// <param name="stop">The stop word: entry 0 is non-zero once the stagnation rule has fired.</param>
    public static void EvaluatePoints<TFunction, TPoint>(
        Index1D index,
        TFunction function,
        StepParameters parameters,
        ArrayView<double> population,
        PointwiseViews<TPoint> points,
        ArrayView<int> stop)
        where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
        where TPoint : unmanaged
    {
        if (stop[0] != 0)
        {
            return;
        }

        int thread = index;
        var individual = thread / points.PointCount;
        var point = thread - individual * points.PointCount;
        var genomeSize = parameters.GenomeSize;
        points.Results[thread] = function.EvaluatePoint(new GeneView(population.SubView(individual * genomeSize, genomeSize)), point);
    }

    /// <summary>
    /// Combines individual <paramref name="index"/>'s point results into its fitness in the current population.
    /// </summary>
    /// <typeparam name="TFunction">The objective.</typeparam>
    /// <typeparam name="TPoint">The result of one point.</typeparam>
    /// <param name="index">The individual.</param>
    /// <param name="function">The objective.</param>
    /// <param name="parameters">D.</param>
    /// <param name="views">The device memory; writes entry <c>i</c> of <c>CurrentFitness</c>.</param>
    /// <param name="points">The device memory; reads results <c>[i·P, (i+1)·P)</c>.</param>
    public static void CombineInitial<TFunction, TPoint>(
        Index1D index,
        TFunction function,
        StepParameters parameters,
        PopulationViews views,
        PointwiseViews<TPoint> points)
        where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
        where TPoint : unmanaged
    {
        int individual = index;
        views.CurrentFitness[individual] = Combine(function, views.Current, individual, parameters.GenomeSize, points);
    }

    /// <summary>
    /// The first part of <see cref="GpuKernels.Generation{TFunction, TRule}"/>: F and CR by <typeparamref name="TRule"/>, then
    /// the trial by the scheme, into slot i of <c>Trial</c>; F and CR are kept in entry i of the trial buffers for the select
    /// kernel. Does nothing once the stop word is set.
    /// </summary>
    /// <typeparam name="TRule">The parameter rule, the one <paramref name="parameters"/> names.</typeparam>
    /// <param name="index">The individual.</param>
    /// <param name="parameters">The seed, the generation, N, D, the scheme and the parameter rule.</param>
    /// <param name="views">The device memory of the population.</param>
    /// <param name="strategy">The device state of the scheme and the rule.</param>
    /// <param name="trialMutationForces">Writes entry i: the F the trial was built with.</param>
    /// <param name="trialCrossoverProbabilities">Writes entry i: the CR the trial was built with.</param>
    public static void BuildTrials<TRule>(
        Index1D index,
        StepParameters parameters,
        PopulationViews views,
        StrategyViews strategy,
        ArrayView<double> trialMutationForces,
        ArrayView<double> trialCrossoverProbabilities)
        where TRule : struct, IControlParameterRule
    {
        if (strategy.Stop[0] != 0)
        {
            return;
        }

        int individual = index;
        var draws = new PhiloxDraws(parameters.Seed, individual, parameters.Generation);
        default(TRule).Draw(ref draws, individual, parameters, strategy, out var mutationForce, out var crossoverProbability);
        var crossoverThreshold = parameters.Rule == ParameterRule.Fixed
            ? parameters.CrossoverThreshold
            : DeStep.CrossoverThreshold(crossoverProbability);
        Schemes.BuildTrial(ref draws, individual, parameters, mutationForce, crossoverThreshold, views, strategy);
        trialMutationForces[individual] = mutationForce;
        trialCrossoverProbabilities[individual] = crossoverProbability;
    }

    /// <summary>
    /// The second part of <see cref="GpuKernels.Generation{TFunction, TRule}"/>: combines individual <paramref name="index"/>'s
    /// point results into its trial's fitness, then <see cref="GpuKernels.SelectAndRecord"/>. Does nothing once the stop word
    /// is set.
    /// </summary>
    /// <typeparam name="TFunction">The objective.</typeparam>
    /// <typeparam name="TPoint">The result of one point.</typeparam>
    /// <param name="index">The individual.</param>
    /// <param name="function">The objective.</param>
    /// <param name="parameters">The parameter rule, the tie rule and D.</param>
    /// <param name="views">The device memory; writes <c>Next</c> and <c>NextFitness</c>.</param>
    /// <param name="strategy">The device state; writes jDE's hand-over or the trial records.</param>
    /// <param name="points">The device memory; reads the trial's point results and the F and CR it was built with.</param>
    public static void Select<TFunction, TPoint>(
        Index1D index,
        TFunction function,
        StepParameters parameters,
        PopulationViews views,
        StrategyViews strategy,
        PointwiseViews<TPoint> points)
        where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
        where TPoint : unmanaged
    {
        if (strategy.Stop[0] != 0)
        {
            return;
        }

        int individual = index;
        var trialFitness = Combine(function, views.Trial, individual, parameters.GenomeSize, points);
        GpuKernels.SelectAndRecord(
            individual,
            trialFitness,
            points.TrialMutationForces[individual],
            points.TrialCrossoverProbabilities[individual],
            parameters,
            views,
            strategy);
    }

    private static double Combine<TFunction, TPoint>(
        TFunction function,
        ArrayView<double> population,
        int individual,
        int genomeSize,
        PointwiseViews<TPoint> points)
        where TFunction : struct, IGpuPointwiseFitnessFunction<TPoint>
        where TPoint : unmanaged =>
        function.Combine(
            new GeneView(population.SubView(individual * genomeSize, genomeSize)),
            new PointView<TPoint>(points.Results.SubView(individual * points.PointCount, points.PointCount)));
}
