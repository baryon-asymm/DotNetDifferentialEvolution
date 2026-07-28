using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Builds a <see cref="ProblemContext"/> and a classic <see cref="AlgorithmExecutor"/> for the
/// worker/orchestrator integration tests.
/// </summary>
internal static class ExecutorFactory
{
    /// <summary>
    /// Creates the context and executor.
    /// </summary>
    /// <remarks>
    /// The seed goes onto the <see cref="ProblemContext"/>; the executor derives one generator per
    /// worker from it, so a seeded run reproduces at any worker count. The existing callers only
    /// seed single-worker runs, which is preserved here.
    /// </remarks>
    public static (ProblemContext Context, AlgorithmExecutor Executor) Create(
        ITestFitnessFunctionEvaluator evaluator,
        ITerminationStrategy terminationStrategy,
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        int workersCount,
        int? seed,
        IGenerationStrategy? generationStrategy = null)
    {
        var useSeed = seed.HasValue && workersCount == 1;

        var context = ProblemContextHelper.CreateContext(
            populationSize, evaluator, terminationStrategy, workersCount, seed: useSeed ? seed : null,
            generationStrategy: generationStrategy);

        var mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability,
            populationSize: populationSize,
            lowerBound: context.GenesLowerBound,
            upperBound: context.GenesUpperBound);
        var selectionStrategy = new SelectionStrategy(context.GenomeSize);
        var executor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);

        return (context, executor);
    }
}
