using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomGenerators.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Helpers;
using DotNetDifferentialEvolution.Tests.RandomGenerators;
using DotNetDifferentialEvolution.WorkerExecutors;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.Tests.WorkerExecutors;

public class WorkerExecutorTester
{
    private readonly IRandomGenerator _randomGenerator;
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IWorkerExecutor _workerExecutor;

    private readonly DEContext _context;
    
    public WorkerExecutorTester()
    {
        var context = DEContextHelper.CreateContext();

        _randomGenerator = new RandomGenerator();
        _mutationStrategy = new MutationStrategy(0.5, 0.9, _randomGenerator, context);
        _selectionStrategy = new SelectionStrategy(context);
        _workerExecutor = new WorkerExecutor(_mutationStrategy,
                                             _selectionStrategy,
                                             context);

        _context = context;
    }

    [Fact]
    public void TestWithSimpleFitnessFunctionEvaluator()
    {
        const int workerId = 0;
        int generations = 10000;

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
        
        var bestValue = _context.PopulationFfValues.Span[bestHandledIndividualIndex];
        
        Assert.Equal(bestValue, -_context.GenomeSize, 1e-6);
    }
}
