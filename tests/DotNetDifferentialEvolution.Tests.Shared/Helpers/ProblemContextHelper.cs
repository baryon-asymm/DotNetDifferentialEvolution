using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.Helpers;

public static class ProblemContextHelper
{
    /// <remarks>
    /// <paramref name="generationStrategy"/> is init-only on the context, so it has to be passed
    /// here; supplying one also routes the orchestrator's best-individual lookup through the
    /// population scan instead of the per-worker indices.
    /// </remarks>
    public static ProblemContext CreateContext(
        int populationSize,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        int workersCount = 1,
        int? seed = null,
        IGenerationStrategy? generationStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(testFitnessFunctionEvaluator);

        var lowerBound = testFitnessFunctionEvaluator.GetLowerBounds();
        var upperBound = testFitnessFunctionEvaluator.GetUpperBounds();

        if (lowerBound.Length != upperBound.Length)
            throw new ArgumentException("Lower and upper bounds must have the same size");

        var boundsSize = lowerBound.Length;
        var populationHelper = new PopulationHelper(populationSize, boundsSize);

        // A seed makes both the initial population and the search reproducible: the context
        // carries it, and the executor derives one generator per worker from it.
        var random = seed.HasValue ? new Random(seed.Value) : null;
        populationHelper.InitializePopulationWithRandomValues(lowerBound.Span, upperBound.Span, random);
        populationHelper.EvaluatePopulationFfValues(testFitnessFunctionEvaluator);

        var context = new ProblemContext(
            populationSize: populationSize,
            genomeSize: boundsSize,
            workersCount: workersCount,
            genesLowerBound: lowerBound,
            genesUpperBound: upperBound,
            fitnessFunctionEvaluator: testFitnessFunctionEvaluator,
            terminationStrategy: terminationStrategy,
            population: populationHelper.Population,
            populationFfValues: populationHelper.PopulationFfValues,
            trialPopulation: populationHelper.TrialPopulation,
            trialPopulationFfValues: populationHelper.TrialPopulationFfValues)
        {
            GenerationStrategy = generationStrategy,
            RandomSeed = seed
        };

        return context;
    }
}
