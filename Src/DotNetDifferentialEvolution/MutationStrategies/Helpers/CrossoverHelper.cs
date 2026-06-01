using DotNetDifferentialEvolution.RandomProviders;

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
    /// trial differs from its parent in at least one dimension — the canonical guarantee
    /// of binomial crossover.
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
                if (trialIndividual[i] < lowerBound[i] || trialIndividual[i] > upperBound[i])
                    trialIndividual[i] =
                        randomProvider.NextDouble() * (upperBound[i] - lowerBound[i]) + lowerBound[i];
            }
            else
            {
                trialIndividual[i] = population[individualOffset + i];
            }
        }
    }
}
