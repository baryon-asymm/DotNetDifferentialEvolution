using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.UnitTests.TestSupport;

/// <summary>
/// Builds small <see cref="Population"/> instances for unit tests without spinning up the
/// full algorithm. The returned population is backed by the supplied arrays, so mutating the
/// fitness array after construction is reflected through the cursor (handy for driving the
/// stagnation strategy across generations).
/// </summary>
internal static class PopulationFactory
{
    /// <summary>
    /// Creates a population from explicit genes and fitness values. The best index defaults to
    /// the position of the minimum fitness value.
    /// </summary>
    public static Population Create(
        double[] genes,
        double[] fitnessValues,
        int? bestIndividualIndex = null,
        int generationNumber = 0,
        long evaluationCount = 0)
    {
        var population = new Population(genes, fitnessValues)
        {
            GenerationNumber = generationNumber,
            EvaluationCount = evaluationCount,
            BestIndividualIndex = bestIndividualIndex ?? IndexOfMinimum(fitnessValues),
        };

        return population;
    }

    /// <summary>Creates a single-individual, single-gene population with the given best fitness.</summary>
    public static Population SingleIndividual(
        double[] fitnessValueBuffer)
    {
        return Create(genes: [0.0], fitnessValues: fitnessValueBuffer, bestIndividualIndex: 0);
    }

    private static int IndexOfMinimum(
        double[] values)
    {
        var best = 0;
        for (int i = 1; i < values.Length; i++)
            if (values[i] < values[best])
                best = i;

        return best;
    }
}
