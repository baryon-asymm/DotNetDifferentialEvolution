using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.Tests.Shared.Helpers;

namespace DotNetDifferentialEvolution.Tests.AlgorithmExecutionTesters;

public class AlgorithmExecutionTester
{
    private readonly BaseRandomProvider _randomProvider;
    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    private readonly IAlgorithmExecutor _algorithmExecutor;

    private readonly ProblemContext _context;
    
    public AlgorithmExecutionTester()
    {
        var context = ProblemContextHelper.CreateContext();

        _randomProvider = new RandomProvider();
        _mutationStrategy = new MutationStrategy(0.5, 0.9, _randomProvider, context);
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
        for (int i = 0; i < generations; i++)
        {
            _algorithmExecutor.Execute(workerId,
                                       out bestHandledIndividualIndex);

            _context.SwapPopulations();
        }

        var population = _context.GetRepresentativePopulation(generations, bestHandledIndividualIndex);
        population.MoveCursorToBestIndividual();
        
        Assert.Equal(population.IndividualCursor.FitnessFunctionValue, -_context.GenomeSize, 1e-6);
    }
}
