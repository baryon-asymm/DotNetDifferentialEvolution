using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;
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
    /// A seeded run is only requested for single-worker tests: the seeded
    /// <see cref="DeterministicRandomProvider"/> wraps a non-thread-safe <see cref="Random"/>,
    /// so multi-worker runs fall back to the thread-safe <see cref="RandomProvider"/>
    /// (<see cref="Random.Shared"/>) and are asserted on convergence tolerance instead of exact
    /// reproduction.
    /// </remarks>
    public static (ProblemContext Context, AlgorithmExecutor Executor) Create(
        ITestFitnessFunctionEvaluator evaluator,
        ITerminationStrategy terminationStrategy,
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        int workersCount,
        int? seed)
    {
        var useSeed = seed.HasValue && workersCount == 1;

        var context = ProblemContextHelper.CreateContext(
            populationSize, evaluator, terminationStrategy, workersCount, seed: useSeed ? seed : null);

        BaseRandomProvider randomProvider = useSeed
            ? new DeterministicRandomProvider(seed!.Value)
            : new RandomProvider();

        var mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability,
            populationSize: populationSize,
            lowerBound: context.GenesLowerBound,
            upperBound: context.GenesUpperBound,
            randomProvider: randomProvider);
        var selectionStrategy = new SelectionStrategy(context.GenomeSize);
        var executor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);

        return (context, executor);
    }
}
