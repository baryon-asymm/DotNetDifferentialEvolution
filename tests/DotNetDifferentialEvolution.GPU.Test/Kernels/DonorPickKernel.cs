using DotNetDifferentialEvolution.GPU.Kernels;
using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// A test kernel for ACCEPTANCE.md, check 1b: thread i runs <see cref="DeStep.PickDonors"/> on its own
/// <see cref="PhiloxDraws"/> stream, many times in a row, and writes every (r1, r2, r3).
/// </summary>
internal static class DonorPickKernel
{
    /// <summary>
    /// Thread i draws <paramref name="picks"/> donor triples from <c>PhiloxDraws(seed, i, 1)</c> and
    /// writes pick k at <c>donors[3·(i·picks + k)]</c>, r1 then r2 then r3.
    /// </summary>
    /// <param name="index">The individual i.</param>
    /// <param name="populationSize">N.</param>
    /// <param name="seed">The seed.</param>
    /// <param name="picks">The number of triples per individual.</param>
    /// <param name="donors">The output, <c>3·N·picks</c> indices.</param>
    public static void PickMany(Index1D index, int populationSize, int seed, int picks, ArrayView<int> donors)
    {
        int individual = index;
        var draws = new PhiloxDraws(seed, individual, 1);
        var offset = 3 * individual * picks;
        for (var k = 0; k < picks; k++)
        {
            DeStep.PickDonors(ref draws, individual, populationSize, out var r1, out var r2, out var r3);
            donors[offset + 3 * k] = r1;
            donors[offset + 3 * k + 1] = r2;
            donors[offset + 3 * k + 2] = r3;
        }
    }
}
