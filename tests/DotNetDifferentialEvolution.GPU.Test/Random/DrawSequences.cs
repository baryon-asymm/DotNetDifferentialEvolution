using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Test.Random;

/// <summary>
/// The draws of one (seed, individual, generation), on the host and from
/// <see cref="GpuKernels.DrawSequence"/> on an accelerator (ACCEPTANCE.md, check 4b).
/// </summary>
internal static class DrawSequences
{
    /// <summary>The number of draws compared, 10⁴.</summary>
    public const int Length = 10_000;

    /// <summary>The first <see cref="Length"/> words of <see cref="PhiloxDraws.NextUInt"/>, on the host.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="individual">The individual.</param>
    /// <param name="generation">The generation.</param>
    /// <returns>The words, in draw order.</returns>
    public static uint[] OnHost(int seed, int individual, int generation)
    {
        var draws = new PhiloxDraws(seed, individual, generation);
        var words = new uint[Length];
        for (var k = 0; k < words.Length; k++)
        {
            words[k] = draws.NextUInt();
        }

        return words;
    }

    /// <summary>The same words, written by <see cref="GpuKernels.DrawSequence"/> on <paramref name="accelerator"/>.</summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="individual">The individual.</param>
    /// <param name="generation">The generation.</param>
    /// <returns>The words, in draw order.</returns>
    public static uint[] OnDevice(Accelerator accelerator, int seed, int individual, int generation)
    {
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, StepParameters, int, ArrayView<uint>>(GpuKernels.DrawSequence);
        using var output = accelerator.Allocate1D<uint>(Length);
        var parameters = new StepParameters(seed, generation, PopulationSize: 1, GenomeSize: 1, MutationForce: 0.0, CrossoverThreshold: 0UL);

        kernel(1, parameters, individual, output.View);
        accelerator.Synchronize();

        var words = new uint[Length];
        output.View.CopyToCPU(words);
        return words;
    }
}
