using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.WorkerExecutors;

public class WorkerExecutor : IWorkerExecutor
{
    private readonly int _populationSize;
    private readonly int _individualHandlerStepSize;

    private readonly Memory<double> _tempIndividual;

    private readonly DEContext _context;

    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;
    
    private readonly IFitnessFunctionEvaluator _fitnessFunctionEvaluator;

    public WorkerExecutor(
        IMutationStrategy mutationStrategy,
        ISelectionStrategy selectionStrategy,
        DEContext context)
    {
        _populationSize = context.PopulationSize;
        _individualHandlerStepSize = context.WorkersCount;

        _tempIndividual = new double[context.GenomeSize];

        _context = context;

        _mutationStrategy = mutationStrategy;
        _selectionStrategy = selectionStrategy;

        _fitnessFunctionEvaluator = context.FitnessFunctionEvaluator;
    }

    public void Execute(
        int workerId,
        out int bestHandledIndividualIndex)
    {
        var tempIndividual = MemoryMarshal.Cast<double, double>(_tempIndividual.Span);

        var population = MemoryMarshal.Cast<double, double>(_context.Population.Span);
        var populationFfValues = MemoryMarshal.Cast<double, double>(_context.PopulationFfValues.Span);
        var tempPopulation = MemoryMarshal.Cast<double, double>(_context.TempPopulation.Span);
        var tempPopulationFfValues = MemoryMarshal.Cast<double, double>(_context.TempPopulationFfValues.Span);

        bestHandledIndividualIndex = workerId;
        for (var i = workerId; i < _populationSize; i += _individualHandlerStepSize)
        {
            _mutationStrategy.Mutate(i, population, tempIndividual);

            var tempIndividualFfValue = _fitnessFunctionEvaluator.Evaluate(tempIndividual);

            _selectionStrategy.Select(individualIndex: i,
                                      tempIndividualFfValue,
                                      tempIndividual,
                                      populationFfValues,
                                      population,
                                      tempPopulationFfValues,
                                      tempPopulation);

            if (tempPopulationFfValues[i] < tempPopulationFfValues[bestHandledIndividualIndex])
                bestHandledIndividualIndex = i;
        }
    }
}
