using DotNetDifferentialEvolution.GPU.Random;
using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// One individual's DE/rand/1/bin step, as static functions over a draw source: donors, mutant,
/// binomial crossover with <c>jrand</c>, midpoint repair, survival. The semantics and the order
/// in which draws are consumed are the CPU package's (<c>docs/ALGORITHMS.md</c>, §§2–3 and §9;
/// <c>RandomIndexSelector</c>, <c>MutationMath</c>, <c>CrossoverHelper</c>), so the same draws
/// give the same trial bit for bit (ACCEPTANCE.md, check 1g).
/// </summary>
internal static class DeStep
{
    private const double TwoToThe64 = 18446744073709551616.0;

    /// <summary>
    /// CR scaled to 64 bits, the CPU package's <c>RandomThreshold.Scale</c>: a gene is crossed
    /// when a uniform 64-bit draw is at most this value.
    /// </summary>
    /// <param name="crossoverProbability">CR, in [0, 1].</param>
    /// <returns>The threshold.</returns>
    public static ulong CrossoverThreshold(double crossoverProbability)
    {
        var scaled = crossoverProbability * TwoToThe64;
        return scaled <= 0.0 ? 0UL : scaled >= TwoToThe64 ? ulong.MaxValue : (ulong)scaled;
    }

    /// <summary>
    /// Draws r1, r2 and r3: mutually distinct and different from <paramref name="individual"/>.
    /// Each is drawn uniformly from the other <c>N − 1</c> indices and redrawn while it repeats an
    /// earlier one.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="populationSize">N; at least 4.</param>
    /// <param name="r1">The base vector's index.</param>
    /// <param name="r2">The minuend's index.</param>
    /// <param name="r3">The subtrahend's index.</param>
    public static void PickDonors<TDraws>(ref TDraws draws, int individual, int populationSize, out int r1, out int r2, out int r3)
        where TDraws : struct, IDrawSource
    {
        r1 = DrawDistinct(ref draws, individual, populationSize, -1, -1, -1, -1);
        r2 = DrawDistinct(ref draws, individual, populationSize, r1, -1, -1, -1);
        r3 = DrawDistinct(ref draws, individual, populationSize, r1, r2, -1, -1);
    }

    /// <summary>
    /// One donor index of the CPU package's <c>RandomIndexSelector.FillDistinctIndices</c>: a draw from the
    /// <c>N − 1</c> indices other than <paramref name="individual"/>, redrawn while it equals one taken before.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="populationSize">N.</param>
    /// <param name="taken0">An index taken before, or −1.</param>
    /// <param name="taken1">An index taken before, or −1.</param>
    /// <param name="taken2">An index taken before, or −1.</param>
    /// <param name="taken3">An index taken before, or −1.</param>
    /// <returns>The index.</returns>
    public static int DrawDistinct<TDraws>(ref TDraws draws, int individual, int populationSize, int taken0, int taken1, int taken2, int taken3)
        where TDraws : struct, IDrawSource
    {
        int candidate;
        do
        {
            candidate = DrawOther(ref draws, individual, populationSize);
        }
        while (candidate == taken0 || candidate == taken1 || candidate == taken2 || candidate == taken3);

        return candidate;
    }

    /// <summary>
    /// Builds the trial of <paramref name="individual"/> into its slot of <paramref name="trial"/>:
    /// <c>v = x_r1 + F·(x_r2 − x_r3)</c>; then gene <c>jrand</c>, and every other gene whose draw
    /// passes CR, keeps the mutant gene, repaired to the midpoint between the violated bound and
    /// the parent's gene; every other gene is the parent's.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="parameters">N, D, F and the crossover threshold.</param>
    /// <param name="population">The current population, read only.</param>
    /// <param name="trial">The trial buffer; only slot i is written.</param>
    /// <param name="lowerBound">The lower bound of each gene.</param>
    /// <param name="upperBound">The upper bound of each gene.</param>
    public static void BuildTrial<TDraws>(
        ref TDraws draws,
        int individual,
        StepParameters parameters,
        ArrayView<double> population,
        ArrayView<double> trial,
        ArrayView<double> lowerBound,
        ArrayView<double> upperBound)
        where TDraws : struct, IDrawSource
    {
        var genomeSize = parameters.GenomeSize;
        PickDonors(ref draws, individual, parameters.PopulationSize, out var r1, out var r2, out var r3);

        var offset = individual * genomeSize;
        var baseOffset = r1 * genomeSize;
        var minuendOffset = r2 * genomeSize;
        var subtrahendOffset = r3 * genomeSize;
        for (var j = 0; j < genomeSize; j++)
        {
            trial[offset + j] = population[baseOffset + j]
                                + parameters.MutationForce * (population[minuendOffset + j] - population[subtrahendOffset + j]);
        }

        CrossAndRepair(ref draws, individual, genomeSize, parameters.CrossoverThreshold, population, trial, lowerBound, upperBound);
    }

    /// <summary>
    /// The CPU package's <c>CrossoverHelper.BinomialCrossoverAndRepair</c> over slot i of <paramref name="trial"/>, which
    /// holds the mutant: <c>jrand</c> is drawn; gene <c>jrand</c>, and every other gene whose 64-bit draw is at most the
    /// threshold, keeps the mutant gene, repaired to the midpoint between the violated bound and the parent's gene;
    /// every other gene is the parent's. No draw is consumed for gene <c>jrand</c>.
    /// </summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="genomeSize">D.</param>
    /// <param name="crossoverThreshold">CR scaled to 64 bits (<see cref="CrossoverThreshold"/>).</param>
    /// <param name="population">The current population, read only.</param>
    /// <param name="trial">The trial buffer; only slot i is read and written.</param>
    /// <param name="lowerBound">The lower bound of each gene.</param>
    /// <param name="upperBound">The upper bound of each gene.</param>
    public static void CrossAndRepair<TDraws>(
        ref TDraws draws,
        int individual,
        int genomeSize,
        ulong crossoverThreshold,
        ArrayView<double> population,
        ArrayView<double> trial,
        ArrayView<double> lowerBound,
        ArrayView<double> upperBound)
        where TDraws : struct, IDrawSource
    {
        var offset = individual * genomeSize;
        var guaranteedGene = draws.NextIndex(genomeSize);
        for (var j = 0; j < genomeSize; j++)
        {
            var parentGene = population[offset + j];
            if (j == guaranteedGene || draws.NextULong() <= crossoverThreshold)
            {
                var mutantGene = trial[offset + j];
                if (mutantGene < lowerBound[j])
                {
                    trial[offset + j] = (lowerBound[j] + parentGene) / 2.0;
                }
                else if (mutantGene > upperBound[j])
                {
                    trial[offset + j] = (upperBound[j] + parentGene) / 2.0;
                }
            }
            else
            {
                trial[offset + j] = parentGene;
            }
        }
    }

    /// <summary>
    /// Whether the trial survives: <c>f(u) ≤ f(x)</c>, with <see cref="double.NaN"/> worse than every
    /// real value; two <see cref="double.NaN"/>s are not a tie, so the parent stays.
    /// </summary>
    /// <param name="trialFitness">f(u).</param>
    /// <param name="parentFitness">f(x).</param>
    /// <returns><see langword="true"/> when the trial replaces the parent.</returns>
    public static bool Survives(double trialFitness, double parentFitness) =>
        trialFitness <= parentFitness || (double.IsNaN(parentFitness) && !double.IsNaN(trialFitness));

    private static int DrawOther<TDraws>(ref TDraws draws, int individual, int populationSize)
        where TDraws : struct, IDrawSource
    {
        var candidate = draws.NextIndex(populationSize - 1);
        return candidate >= individual ? candidate + 1 : candidate;
    }
}
