using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomGenerators.Interfaces;
using DotNetDifferentialEvolution.Tests.Helpers;
using DotNetDifferentialEvolution.Tests.RandomGenerators;
using DotNetDifferentialEvolution.WorkerExecutors;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.Tests.WorkerExecutors;

public class WorkerExecutorTester
{
    private readonly Memory<double> _populationFfValues;
    private readonly Memory<double> _population;
    private readonly Memory<double> _bufferPopulationFfValues;
    private readonly Memory<double> _bufferPopulation;

    private readonly IRandomGenerator _randomGenerator;
    private readonly IWorkerExecutor _workerExecutor;
    private readonly IMutationStrategy _mutationStrategy;
    
    public WorkerExecutorTester()
    {
        var context = DEContextHelper.CreateContext();
        var populationHelper = new PopulationHelper(context.PopulationSize, context.GenomeSize);
        
        populationHelper.InitializePopulationWithRandomValues();

        _populationFfValues = populationHelper.PopulationFfValues;
        _population = populationHelper.Population;
        _bufferPopulationFfValues = populationHelper.BufferPopulationFfValues;
        _bufferPopulation = populationHelper.BufferPopulation;

        _randomGenerator = new RandomGenerator();
        _mutationStrategy = new MutationStrategy(0.3, 0.8, _randomGenerator, context);
        _workerExecutor = new WorkerExecutor(_populationFfValues, _population, _bufferPopulationFfValues,
            _bufferPopulation, _mutationStrategy, context);
    }

    [Fact]
    public void TestWithSimpleFitnessFunctionEvaluator()
    {
        const int workerId = 0;
        _workerExecutor.Execute(workerId);
    }
}