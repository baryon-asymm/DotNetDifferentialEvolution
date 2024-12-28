using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.AlgorithmExecutors;

public class AlgorithmExecutor : IAlgorithmExecutor
{
    private readonly int _populationSize;
    private readonly int _individualHandlerStepSize;

    private readonly Memory<double> _trialIndividual;

    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;

    private readonly ProblemContext _context;

    public AlgorithmExecutor(
        IMutationStrategy mutationStrategy,
        ISelectionStrategy selectionStrategy,
        ProblemContext context)
    {
        _populationSize = context.PopulationSize;
        _individualHandlerStepSize = context.WorkersCount;

        _trialIndividual = new double[context.GenomeSize];

        _mutationStrategy = mutationStrategy;
        _selectionStrategy = selectionStrategy;

        _context = context;
    }

    public void Execute(
        int workerId,
        out int bestHandledIndividualIndex)
    {
        var trialIndividual = _trialIndividual.Span;

        var population = _context.Population.Span;
        var populationFfValues = _context.PopulationFfValues.Span;
        var nextPopulation = _context.TrialPopulation.Span;
        var nextPopulationFfValues = _context.TrialPopulationFfValues.Span;

        var fitnessFunctionEvaluator = _context.FitnessFunctionEvaluator;

        bestHandledIndividualIndex = workerId;
        for (var i = workerId; i < _populationSize; i += _individualHandlerStepSize)
        {
            _mutationStrategy.Mutate(
                individualIndex: i,
                population: population,
                trialIndividual: trialIndividual);

            var trialIndividualFfValue = fitnessFunctionEvaluator.Evaluate(trialIndividual);

            _selectionStrategy.Select(
                individualIndex: i,
                trialIndividualFfValue: trialIndividualFfValue,
                trialIndividual: trialIndividual,
                populationFfValues: populationFfValues,
                population: population,
                nextPopulationFfValues: nextPopulationFfValues,
                nextPopulation: nextPopulation);

            if (nextPopulationFfValues[i] < nextPopulationFfValues[bestHandledIndividualIndex])
                bestHandledIndividualIndex = i;
        }
    }
}
