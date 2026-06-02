using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies.Helpers;

/// <summary>
/// Helpers for drawing distinct random individual indices for difference-vector construction.
/// </summary>
internal static class RandomIndexSelector
{
    /// <summary>
    /// Fills <paramref name="indices"/> with mutually distinct random indices in
    /// <c>[0, populationSize)</c>, each different from <paramref name="excludeIndex"/>.
    /// </summary>
    public static void FillDistinctIndices(
        Span<int> indices,
        int populationSize,
        int excludeIndex,
        BaseRandomProvider randomProvider)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            int candidate;
            bool isUnique;
            do
            {
                candidate = randomProvider.Next(populationSize - 1);
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
