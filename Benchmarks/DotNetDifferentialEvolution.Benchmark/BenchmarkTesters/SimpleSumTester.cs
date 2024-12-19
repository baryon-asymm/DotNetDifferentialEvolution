using BenchmarkDotNet.Attributes;
using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Benchmark.RandomGenerators;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomGenerators.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.Benchmark.BenchmarkTesters;

public class SimpleSumTester
{
    private readonly IRandomGenerator _randomGenerator;
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IAlgorithmExecutor _algorithmExecutor;
    
    private readonly DEContext _context;
    
    public SimpleSumTester()
    {
        var context = DEContextHelper.CreateContext();

        const int seed = 0x12345678;
        _randomGenerator = new DeterminedRandomGenerator(seed);
        _mutationStrategy = new MutationStrategy(0.5, 0.9, _randomGenerator, context);
        _selectionStrategy = new SelectionStrategy(context);
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
