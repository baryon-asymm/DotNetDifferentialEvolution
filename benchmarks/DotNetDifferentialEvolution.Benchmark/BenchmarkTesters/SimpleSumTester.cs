using BenchmarkDotNet.Attributes;
using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Common.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Common.Helpers;

namespace DotNetDifferentialEvolution.Benchmark.BenchmarkTesters;

/// <summary>
/// The throughput of one generation of the engine: classic DE/rand/1/bin with greedy selection
/// on a 20-gene sum objective, population 300, one worker, seeded so every run measures the same
/// work.
/// </summary>
public class SimpleSumTester
{
    private readonly AlgorithmExecutor _algorithmExecutor;

    /// <summary>
    /// Builds the context, the strategies and the executor once, outside the measured method.
    /// </summary>
    public SimpleSumTester()
    {
        const int genomeSize = 20;
        var lowerBounds = new double[genomeSize];
        var upperBounds = new double[genomeSize];
        for (var i = 0; i < genomeSize; i++)
        {
            lowerBounds[i] = -10;
            upperBounds[i] = 10;
        }

        var evaluator = new SimpleSumEvaluator(lowerBounds, upperBounds);
        const int maxGenerationNumber = 100; // Not used in this benchmark
        var terminationStrategy = new LimitGenerationNumberTerminationStrategy(maxGenerationNumber); // ...also not used
        const int populationSize = 300;
        // Seeded so the benchmark measures the same work every run; the executor derives the
        // worker's generator from it.
        var context = ProblemContextHelper.CreateContext(
            populationSize, evaluator, terminationStrategy, seed: 0x12345678);

        const double mutationForce = 0.5;
        const double crossoverProbability = 0.9;

        var mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability);
        var selectionStrategy = new SelectionStrategy(genomeSize);
        _algorithmExecutor = new AlgorithmExecutor(mutationStrategy, selectionStrategy, context);
    }

    /// <summary>
    /// One generation's worth of work for worker 0: mutation, crossover, evaluation and
    /// selection of every individual.
    /// </summary>
    [Benchmark]
    public void SimpleFitnessFunctionEvaluatorBenchmark()
    {
        const int workerId = 0;
        _algorithmExecutor.Execute(workerId, out _);
    }
}
