using BenchmarkDotNet.Attributes;
using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Benchmark.RandomGenerators;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.Benchmark.BenchmarkTesters;

public class SimpleSumTester
{
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IAlgorithmExecutor _algorithmExecutor;
    
    private readonly ProblemContext _context;
    
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
        
        _mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability,
            populationSize: populationSize,
            lowerBound: context.GenesLowerBound,
            upperBound: context.GenesUpperBound);
        _selectionStrategy = new SelectionStrategy(genomeSize);
        _algorithmExecutor = new AlgorithmExecutor(_mutationStrategy,
                                                   _selectionStrategy,
                                                   context);

        _context = context;
    }
    
    [Benchmark]
    public void SimpleFitnessFunctionEvaluatorBenchmark()
    {
        const int workerId = 0;
        _algorithmExecutor.Execute(workerId, out _);
    }
}
