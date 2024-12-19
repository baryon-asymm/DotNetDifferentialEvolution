using BenchmarkDotNet.Attributes;
using DotNetDifferentialEvolution.Benchmark.RandomGenerators;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomGenerators.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;
using DotNetDifferentialEvolution.WorkerExecutors;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.Benchmark.BenchmarkTesters;

public class SimpleSumTester
{
    private readonly IRandomGenerator _randomGenerator;
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IWorkerExecutor _workerExecutor;
    
    private readonly DEContext _context;
    
    public SimpleSumTester()
    {
        var context = DEContextHelper.CreateContext();

        const int seed = 0x12345678;
        _randomGenerator = new DeterminedRandomGenerator(seed);
        _mutationStrategy = new MutationStrategy(0.5, 0.9, _randomGenerator, context);
        _selectionStrategy = new SelectionStrategy(context);
        _workerExecutor = new WorkerExecutor(_mutationStrategy,
                                             _selectionStrategy,
                                             context);

        _context = context;
    }
    
    [Benchmark]
    public void SimpleFitnessFunctionEvaluatorBenchmark()
    {
        const int workerId = 0;
        int generations = 1000;

        int bestHandledIndividualIndex = 0;
        while (generations-- > 0)
        {
            _workerExecutor.Execute(workerId,
                                    out bestHandledIndividualIndex);

            var p = _context.Population;
            var ff = _context.PopulationFfValues;
            _context.Population = _context.TempPopulation;
            _context.PopulationFfValues = _context.TempPopulationFfValues;
            _context.TempPopulation = p;
            _context.TempPopulationFfValues = ff;
        }
    }
}
