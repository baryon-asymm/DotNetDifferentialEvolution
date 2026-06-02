
namespace DotNetDifferentialEvolution.MutationStrategies.Helpers;

/// <summary>
/// Shared binomial crossover and bound-repair logic used by every mutation strategy.
/// </summary>
internal static class CrossoverHelper
{
    /// <summary>
    /// Applies binomial crossover between the freshly built mutant vector (in
    /// <paramref name="trialIndividual"/>) and the target individual, then repairs any
    /// out-of-bound genes that were inherited from the mutant.
    /// </summary>
    /// <remarks>
    /// A randomly chosen gene index (<c>jrand</c>) is always taken from the mutant so the
    /// trial differs from its parent in at least one dimension — the canonical guarantee of
    /// binomial crossover. An out-of-bound gene is reflected halfway back toward the parent
    /// gene (<c>(bound + x_i) / 2</c>), the repair rule of the JADE/SHADE/L-SHADE papers.
    /// </remarks>
    public static void BinomialCrossoverAndRepair(
        int individualIndex,
        double crossoverProbability,
        ReadOnlySpan<double> population,
        Span<double> trialIndividual,
        ReadOnlySpan<double> lowerBound,
        ReadOnlySpan<double> upperBound,
        BaseRandomProvider randomProvider)
    {
        var genomeSize = trialIndividual.Length;
        var guaranteedGeneIndex = randomProvider.Next(genomeSize);
        var individualOffset = individualIndex * genomeSize;

        for (int i = 0; i < genomeSize; i++)
        {
            if (i == guaranteedGeneIndex || randomProvider.NextDouble() <= crossoverProbability)
            {
                var parentGene = population[individualOffset + i];
                if (trialIndividual[i] < lowerBound[i])
                    trialIndividual[i] = (lowerBound[i] + parentGene) / 2.0;
                else if (trialIndividual[i] > upperBound[i])
                    trialIndividual[i] = (upperBound[i] + parentGene) / 2.0;
            }
            else
            {
                trialIndividual[i] = population[individualOffset + i];
            }
        }
    }
}
