using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Common.Helpers;

/// <summary>
/// Builds a <see cref="ProblemContext"/> by hand, without the builder, for tests that drive the
/// engine's parts directly: the population is sampled uniformly within the evaluator's bounds and
/// evaluated once.
/// </summary>
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
        IGenerationStrategy? generationStrategy = null) =>
        Create(populationSize, testFitnessFunctionEvaluator, terminationStrategy, workersCount, seed,
            generationStrategy, controlParameterProvider: null);

    /// <remarks>
    /// <paramref name="controlParameterProvider"/> is init-only on the context, so it has to be
    /// passed here. Passing null builds the context the builder would refuse for a strategy that
    /// reads F and CR, which is what the executor's own guard is tested with. A separate overload,
    /// not an optional parameter, so that callers of the other one do not name the provider type.
    /// </remarks>
    public static ProblemContext CreateContext(
        int populationSize,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        IControlParameterProvider? controlParameterProvider,
        int? seed = null) =>
        Create(populationSize, testFitnessFunctionEvaluator, terminationStrategy, workersCount: 1, seed,
            generationStrategy: null, controlParameterProvider);

    private static ProblemContext Create(
        int populationSize,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        int workersCount,
        int? seed,
        IGenerationStrategy? generationStrategy,
        IControlParameterProvider? controlParameterProvider)
    {
        ArgumentNullException.ThrowIfNull(testFitnessFunctionEvaluator);

        var lowerBound = testFitnessFunctionEvaluator.GetLowerBounds();
        var upperBound = testFitnessFunctionEvaluator.GetUpperBounds();

        if (lowerBound.Length != upperBound.Length)
        {
            throw new ArgumentException("Lower and upper bounds must have the same size");
        }

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
            ControlParameterProvider = controlParameterProvider,
            RandomSeed = seed
        };

        return context;
    }
}
