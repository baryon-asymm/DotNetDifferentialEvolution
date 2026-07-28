using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies.Helpers;

/// <summary>
/// Shared binomial crossover and bound-repair logic used by every mutation strategy.
/// </summary>
internal static class CrossoverHelper
{
    /// <summary>
    /// Applies binomial crossover and bound repair for the individual described by
    /// <paramref name="context"/>, drawing from the worker's own generator when the engine
    /// supplied one.
    /// </summary>
    /// <param name="context">The trial being built.</param>
    /// <param name="crossoverProbability">
    /// The crossover probability to apply — the strategy's own, which is not always the one
    /// carried on the context.
    /// </param>
    public static void BinomialCrossoverAndRepair(
        in MutationContext context,
        double crossoverProbability)
    {
        if (context.WorkerRandomProvider is { } workerRandom)
        {
            BinomialCrossoverAndRepair(
                context.IndividualIndex, crossoverProbability, context.Population,
                context.TrialIndividual, context.LowerBound, context.UpperBound,
                new SeededRandomSource(workerRandom));
        }
        else
        {
            BinomialCrossoverAndRepair(
                context.IndividualIndex, crossoverProbability, context.Population,
                context.TrialIndividual, context.LowerBound, context.UpperBound,
                new ProviderRandomSource(context.RandomProvider));
        }
    }

    /// <summary>
    /// Applies binomial crossover between the freshly built mutant vector (in
    /// <paramref name="trialIndividual"/>) and the target individual, then repairs any
    /// out-of-bound genes that were inherited from the mutant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A randomly chosen gene index (<c>jrand</c>) is always taken from the mutant so the
    /// trial differs from its parent in at least one dimension — the canonical guarantee of
    /// binomial crossover. An out-of-bound gene is reflected halfway back toward the parent
    /// gene (<c>(bound + x_i) / 2</c>), the repair rule of the JADE/SHADE/L-SHADE papers.
    /// </para>
    /// <para>
    /// This loop runs once per gene of every trial of every generation, which makes it the
    /// hottest code in the library. Two things follow from that. The per-gene test is an integer
    /// comparison against a threshold scaled once per call, not a floating-point comparison
    /// against a freshly converted uniform. And <typeparamref name="TRandom"/> is a
    /// <see langword="struct"/>, so the draw is inlined here rather than dispatched.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRandom">The source of randomness, supplied by value so its calls bind statically.</typeparam>
    public static void BinomialCrossoverAndRepair<TRandom>(
        int individualIndex,
        double crossoverProbability,
        ReadOnlySpan<double> population,
        Span<double> trialIndividual,
        ReadOnlySpan<double> lowerBound,
        ReadOnlySpan<double> upperBound,
        TRandom randomSource)
        where TRandom : struct, IRandomSource
    {
        var genomeSize = trialIndividual.Length;
        var guaranteedGeneIndex = randomSource.Next(genomeSize);
        var individualOffset = individualIndex * genomeSize;
        var crossoverThreshold = RandomThreshold.Scale(crossoverProbability);

        for (int i = 0; i < genomeSize; i++)
        {
            if (i == guaranteedGeneIndex || randomSource.NextULong() <= crossoverThreshold)
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
