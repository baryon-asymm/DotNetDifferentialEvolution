using DotNetDifferentialEvolution.GPU.Objectives;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The kernel entry points of the package, one thread per individual. A kernel is any method
/// whose first parameter is an <see cref="Index1D"/>; the guards of ACCEPTANCE.md, §8, walk
/// everything they reach.
/// </summary>
internal static class GpuKernels
{
    /// <summary>
    /// Samples individual <paramref name="index"/> uniformly in the box from its generation-0 draws,
    /// <c>lower + u·(upper − lower)</c> with <c>u</c> in <c>[0, 1)</c>, and evaluates it into the
    /// current population.
    /// </summary>
    /// <typeparam name="TFunction">The objective.</typeparam>
    /// <param name="index">The individual.</param>
    /// <param name="function">The objective.</param>
    /// <param name="parameters">The seed, N and D.</param>
    /// <param name="views">The device memory; writes <c>Current</c> and <c>CurrentFitness</c>.</param>
    public static void Initialize<TFunction>(Index1D index, TFunction function, StepParameters parameters, PopulationViews views)
        where TFunction : struct, IGpuFitnessFunction
    {
        int individual = index;
        var draws = new PhiloxDraws(parameters.Seed, individual, 0);
        var genomeSize = parameters.GenomeSize;
        var offset = individual * genomeSize;
        for (var j = 0; j < genomeSize; j++)
        {
            var lower = views.LowerBound[j];
            views.Current[offset + j] = lower + draws.NextUnitDouble() * (views.UpperBound[j] - lower);
        }

        views.CurrentFitness[individual] = function.Evaluate(new GeneView(views.Current.SubView(offset, genomeSize)));
    }

    /// <summary>
    /// One generation for individual <paramref name="index"/>: builds its trial from the current
    /// population (<see cref="DeStep.BuildTrial"/>), evaluates it, and writes the survivor and its
    /// fitness into slot i of the next population. Reads every slot of the current population and
    /// writes only slot i of the trial and next populations, so a launch needs no synchronisation.
    /// </summary>
    /// <typeparam name="TFunction">The objective.</typeparam>
    /// <param name="index">The individual.</param>
    /// <param name="function">The objective.</param>
    /// <param name="parameters">The seed, the generation, N, D, F and the crossover threshold.</param>
    /// <param name="views">The device memory.</param>
    public static void Generation<TFunction>(Index1D index, TFunction function, StepParameters parameters, PopulationViews views)
        where TFunction : struct, IGpuFitnessFunction
    {
        int individual = index;
        var draws = new PhiloxDraws(parameters.Seed, individual, parameters.Generation);
        DeStep.BuildTrial(ref draws, individual, parameters, views.Current, views.Trial, views.LowerBound, views.UpperBound);

        var genomeSize = parameters.GenomeSize;
        var offset = individual * genomeSize;
        var trialFitness = function.Evaluate(new GeneView(views.Trial.SubView(offset, genomeSize)));
        var parentFitness = views.CurrentFitness[individual];
        var trialSurvives = DeStep.Survives(trialFitness, parentFitness);
        var survivors = trialSurvives ? views.Trial : views.Current;
        for (var j = 0; j < genomeSize; j++)
        {
            views.Next[offset + j] = survivors[offset + j];
        }

        views.NextFitness[individual] = trialSurvives ? trialFitness : parentFitness;
    }

    /// <summary>
    /// Writes the first <c>output.Length</c> words of the draws of one (seed, individual,
    /// generation): the evidence that every backend draws the same words (ACCEPTANCE.md, check
    /// 4b). Launched with one thread.
    /// </summary>
    /// <param name="index">The thread; only thread 0 writes.</param>
    /// <param name="parameters">The seed and the generation.</param>
    /// <param name="individual">The individual whose draws are written.</param>
    /// <param name="output">The words, in draw order.</param>
    public static void DrawSequence(Index1D index, StepParameters parameters, int individual, ArrayView<uint> output)
    {
        if (index == 0)
        {
            var draws = new PhiloxDraws(parameters.Seed, individual, parameters.Generation);
            for (var k = 0; k < output.IntLength; k++)
            {
                output[k] = draws.NextUInt();
            }
        }
    }

    /// <summary>
    /// Philox4x32-10 inside a kernel: thread i turns counter i and key i into block i (ACCEPTANCE.md,
    /// check 3a, on the device).
    /// </summary>
    /// <param name="index">The block.</param>
    /// <param name="counters">The counters, four words each.</param>
    /// <param name="keys">The keys, two words each.</param>
    /// <param name="blocks">The output blocks, four words each.</param>
    public static void PhiloxBlocks(Index1D index, ArrayView<uint> counters, ArrayView<uint> keys, ArrayView<uint> blocks)
    {
        int i = index;
        var block = Philox4x32x10.Generate(
            new PhiloxBlock(counters[4 * i], counters[4 * i + 1], counters[4 * i + 2], counters[4 * i + 3]),
            keys[2 * i],
            keys[2 * i + 1]);
        blocks[4 * i] = block.X0;
        blocks[4 * i + 1] = block.X1;
        blocks[4 * i + 2] = block.X2;
        blocks[4 * i + 3] = block.X3;
    }
}
