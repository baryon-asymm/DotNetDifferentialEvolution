using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomGenerators.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;
using DotNetDifferentialEvolution.Tests.Shared.RandomGenerators;

namespace DotNetDifferentialEvolution.Tests.AlgorithmExecutionTesters;

public class AlgorithmExecutionTester
{
    private readonly IRandomGenerator _randomGenerator;
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IAlgorithmExecutor _algorithmExecutor;

    private readonly DEContext _context;
    
    public AlgorithmExecutionTester()
    {
        var context = DEContextHelper.CreateContext();

        _randomGenerator = new RandomGenerator();
        _mutationStrategy = new MutationStrategy(0.5, 0.9, _randomGenerator, context);
        _selectionStrategy = new SelectionStrategy(context);
        _algorithmExecutor = new AlgorithmExecutor(_mutationStrategy,
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
            _algorithmExecutor.Execute(workerId,
                                    out bestHandledIndividualIndex);

            var p = _context.Population;
            var ff = _context.PopulationFfValues;
            _context.Population = _context.TrialPopulation;
            _context.PopulationFfValues = _context.TrialPopulationFfValues;
            _context.TrialPopulation = p;
            _context.TrialPopulationFfValues = ff;
        }
        
        var bestValue = _context.PopulationFfValues.Span[bestHandledIndividualIndex];
        
        Assert.Equal(bestValue, -_context.GenomeSize, 1e-6);
    }
}
