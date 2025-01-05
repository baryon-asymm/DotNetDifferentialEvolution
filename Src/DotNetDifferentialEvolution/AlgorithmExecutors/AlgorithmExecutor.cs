using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.AlgorithmExecutors;

/// <summary>
/// Class for executing the differential evolution algorithm.
/// </summary>
public class AlgorithmExecutor : IAlgorithmExecutor
{
    private readonly int _populationSize;
    private readonly int _individualHandlerStepSize;

    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;

    private readonly ProblemContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmExecutor"/> class.
    /// </summary>
    /// <param name="mutationStrategy">The mutation strategy to be used.</param>
    /// <param name="selectionStrategy">The selection strategy to be used.</param>
    /// <param name="context">The problem context containing population and other parameters.</param>
    public AlgorithmExecutor(
        IMutationStrategy mutationStrategy,
        ISelectionStrategy selectionStrategy,
        ProblemContext context)
    {
        _populationSize = context.PopulationSize;
        _individualHandlerStepSize = context.WorkersCount;

        _mutationStrategy = mutationStrategy;
        _selectionStrategy = selectionStrategy;

        _context = context;
    }

    /// <summary>
    /// Executes the algorithm.
    /// </summary>
    /// <param name="workerId">The index of the worker executing the algorithm.</param>
    /// <param name="bestHandledIndividualIndex">The index of the best handled individual.</param>
    public void Execute(
        int workerId,
        out int bestHandledIndividualIndex)
    {
        Span<double> trialIndividual = stackalloc double[_context.GenomeSize];

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

            var trialIndividualFfValue = fitnessFunctionEvaluator.Evaluate(
                workerIndex: workerId,
                genes: trialIndividual);

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
