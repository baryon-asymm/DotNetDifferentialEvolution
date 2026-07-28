using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies.Helpers;

/// <summary>
/// Helpers for drawing distinct random individual indices for difference-vector construction.
/// </summary>
internal static class RandomIndexSelector
{
    /// <summary>
    /// Fills <paramref name="indices"/> with mutually distinct indices drawn from the population
    /// described by <paramref name="context"/>, none of them the individual being mutated,
    /// drawing from the worker's own generator when the engine supplied one.
    /// </summary>
    public static void FillDistinctIndices(
        Span<int> indices,
        in MutationContext context)
    {
        if (context.WorkerRandomProvider is { } workerRandom)
        {
            FillDistinctIndices(
                indices, context.PopulationSize, context.IndividualIndex,
                new SeededRandomSource(workerRandom));
        }
        else
        {
            FillDistinctIndices(
                indices, context.PopulationSize, context.IndividualIndex,
                new ProviderRandomSource(context.RandomProvider));
        }
    }

    /// <summary>
    /// Fills <paramref name="indices"/> with mutually distinct random indices in
    /// <c>[0, populationSize)</c>, each different from <paramref name="excludeIndex"/>.
    /// </summary>
    /// <typeparam name="TRandom">The source of randomness, supplied by value so its calls bind statically.</typeparam>
    public static void FillDistinctIndices<TRandom>(
        Span<int> indices,
        int populationSize,
        int excludeIndex,
        TRandom randomSource)
        where TRandom : struct, IRandomSource
    {
        for (int i = 0; i < indices.Length; i++)
        {
            int candidate;
            bool isUnique;
            do
            {
                candidate = randomSource.Next(populationSize - 1);
                if (candidate >= excludeIndex) candidate++;

                isUnique = true;
                for (int j = 0; j < i; j++)
                {
                    if (indices[j] == candidate)
                    {
                        isUnique = false;
                        break;
                    }
                }
            } while (isUnique == false);

            indices[i] = candidate;
        }
    }
}
