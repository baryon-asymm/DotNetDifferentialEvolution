using System.Runtime.InteropServices;
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
    private readonly Memory<double> _bufferPopulation;
    private readonly Memory<double> _bufferPopulationFfValues;
    private readonly Memory<double> _population;
    private readonly Memory<double> _populationFfValues;

    private readonly IRandomGenerator _randomGenerator;
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IWorkerExecutor _workerExecutor;

    public WorkerExecutorTester()
    {
        var context = DEContextHelper.CreateContext();
        var populationHelper = new PopulationHelper(context.PopulationSize, context.GenomeSize);

        populationHelper.InitializePopulationWithRandomValues();
        populationHelper.EvaluatePopulationFfValues(context.FitnessFunctionEvaluator);

        _populationFfValues = populationHelper.PopulationFfValues;
        _population = populationHelper.Population;
        _bufferPopulationFfValues = populationHelper.BufferPopulationFfValues;
        _bufferPopulation = populationHelper.BufferPopulation;

        _randomGenerator = new RandomGenerator();
        _mutationStrategy = new MutationStrategy(0.5, 0.9, _randomGenerator, context);
        _selectionStrategy = new SelectionStrategy(context);
        _workerExecutor = new WorkerExecutor(_mutationStrategy,
                                             _selectionStrategy,
                                             context);
    }

    [Fact]
    public void TestWithSimpleFitnessFunctionEvaluator()
    {
        const int workerId = 0;
        int generations = 50000;
        
        var population = _population.Span;
        var populationFfValues = _populationFfValues.Span;
        var bufferPopulation = _bufferPopulation.Span;
        var bufferPopulationFfValues = _bufferPopulationFfValues.Span;

        int bestHandledIndividualIndex = 0;
        while (generations-- > 0)
        {
            _workerExecutor.Execute(workerId,
                                    population,
                                    populationFfValues,
                                    bufferPopulation,
                                    bufferPopulationFfValues,
                                    out bestHandledIndividualIndex);
            
            population = bufferPopulation;
            populationFfValues = bufferPopulationFfValues;
        }
        
        var bestValue = populationFfValues[bestHandledIndividualIndex];
        var a = 0;
    }
}
