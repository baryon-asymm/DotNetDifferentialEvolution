using DotNetDifferentialEvolution.GPU.Test.Random;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Runtime.CPU;

namespace DotNetDifferentialEvolution.GPU.Test.Kernels;

/// <summary>
/// ACCEPTANCE.md, check 1b: the donor indices drawn by <c>DeStep.PickDonors</c> inside a kernel on
/// ILGPU's CPU accelerator (<see cref="DonorPickKernel"/>), for N = 4 and N = 50, 10⁵ triples per
/// individual i.
/// <list type="bullet">
/// <item>r1, r2 and r3 are mutually distinct and never i.</item>
/// <item>
/// Uniformity: for each role (r1, r2, r3) the counts over every (i, index ≠ i) cell, expected
/// 10⁵ / (N − 1) each, give one χ² statistic with N·(N − 2) degrees of freedom (each row i has N − 1
/// cells and a fixed total): df = 8 for N = 4 and df = 2400 for N = 50. It stays under the 0.999
/// quantile of <see cref="ChiSquared.Quantile"/>. One joint test per (N, role), six in all, rather
/// than one per (N, i, role), which would be 162 tests at 0.999 each.
/// </item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public class DonorPickTests
{
    private const int Picks = 100_000;
    private const int Seed = 4242;

    /// <summary>The three donors are distinct, never i, and each role is uniform over the others.</summary>
    /// <param name="populationSize">N.</param>
    [Theory]
    [InlineData(4)]
    [InlineData(50)]
    public void DonorsAreDistinctNeverTheIndividualAndUniform(int populationSize)
    {
        var donors = Draw(populationSize);
        var counts = new long[3][];
        for (var role = 0; role < 3; role++)
        {
            counts[role] = new long[populationSize * populationSize];
        }

        var violations = 0;
        for (var i = 0; i < populationSize; i++)
        {
            for (var k = 0; k < Picks; k++)
            {
                var at = 3 * (i * Picks + k);
                int r1 = donors[at], r2 = donors[at + 1], r3 = donors[at + 2];
                if (r1 == i || r2 == i || r3 == i || r1 == r2 || r1 == r3 || r2 == r3)
                {
                    violations++;
                }

                counts[0][i * populationSize + r1]++;
                counts[1][i * populationSize + r2]++;
                counts[2][i * populationSize + r3]++;
            }
        }

        Assert.Equal(0, violations);

        var degreesOfFreedom = populationSize * (populationSize - 2);
        var quantile = ChiSquared.Quantile(0.999, degreesOfFreedom);
        for (var role = 0; role < 3; role++)
        {
            var admissible = new List<long>();
            for (var i = 0; i < populationSize; i++)
            {
                for (var r = 0; r < populationSize; r++)
                {
                    if (r != i)
                    {
                        admissible.Add(counts[role][i * populationSize + r]);
                    }
                }
            }

            var statistic = ChiSquared.Statistic([.. admissible], (double)Picks / (populationSize - 1));
            Assert.True(statistic < quantile, $"N = {populationSize}, r{role + 1}: χ² = {statistic}, 0.999 quantile (df = {degreesOfFreedom}) = {quantile}");
        }
    }

    private static int[] Draw(int populationSize)
    {
        using var context = Context.Create(builder => builder.CPU());
        using var accelerator = context.CreateCPUAccelerator(0);
        var kernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, int, int, int, ArrayView<int>>(DonorPickKernel.PickMany);
        var length = 3 * populationSize * Picks;
        using var buffer = accelerator.Allocate1D<int>(length);

        kernel(populationSize, populationSize, Seed, Picks, buffer.View);
        accelerator.Synchronize();

        var donors = new int[length];
        buffer.View.CopyToCPU(donors);
        return donors;
    }
}
