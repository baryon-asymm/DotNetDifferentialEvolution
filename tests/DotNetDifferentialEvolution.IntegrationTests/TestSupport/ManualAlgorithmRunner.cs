using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Fakes;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.IntegrationTests.TestSupport;

/// <summary>
/// Drives the classic <see cref="AlgorithmExecutor"/> generation loop on a single thread with
/// a seeded random provider and a seeded initial population. The whole run is therefore
/// deterministic and reproducible from the seed — the right level for integration-testing the
/// executor + selection + termination wiring without the worker-thread machinery.
/// </summary>
internal static class ManualAlgorithmRunner
{
    public static Population Run(
        ITestFitnessFunctionEvaluator evaluator,
        ITerminationStrategy terminationStrategy,
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        int seed)
    {
        var context = ProblemContextHelper.CreateContext(
            populationSize, evaluator, terminationStrategy, workersCount: 1, seed: seed);

        // A non-thread-safe seeded provider is safe here because the loop is single-threaded.
        var randomProvider = new DeterministicRandomProvider(seed);
        var mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability,
            populationSize: populationSize,
            lowerBound: context.GenesLowerBound,
            upperBound: context.GenesUpperBound,
            randomProvider: randomProvider);
        var selectionStrategy = new SelectionStrategy(context.GenomeSize);
        var executor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);

        Population population;
        var generationNumber = 0;
        do
        {
            executor.Execute(workerId: 0, out var bestHandledIndividualIndex);
            context.SwapPopulations();
            population = context.GetRepresentativePopulation(++generationNumber, bestHandledIndividualIndex);
        } while (terminationStrategy.ShouldTerminate(population) == false);

        population.MoveCursorToBestIndividual();
        return population;
    }
}
