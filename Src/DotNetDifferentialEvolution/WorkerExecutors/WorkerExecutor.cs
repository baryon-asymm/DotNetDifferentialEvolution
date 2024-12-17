using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.WorkerExecutors;

public class WorkerExecutor : IWorkerExecutor
{
    private readonly IFitnessFunctionEvaluator _fitnessFunctionEvaluator;
    private readonly int _genomeSize;

    private readonly int _individualHandlerStepSize;

    private readonly IMutationStrategy _mutationStrategy;

    private readonly ISelectionStrategy _selectionStrategy;
    private readonly int _populationSize;

    private readonly Memory<double> _tempIndividual;

    public WorkerExecutor(
        IMutationStrategy mutationStrategy,
        ISelectionStrategy selectionStrategy,
        DEContext context)
    {
        _populationSize = context.PopulationSize;
        _genomeSize = context.GenomeSize;

        _individualHandlerStepSize = context.WorkersCount;

        _mutationStrategy = mutationStrategy;
        _selectionStrategy = selectionStrategy;

        _fitnessFunctionEvaluator = context.FitnessFunctionEvaluator;

        _tempIndividual = new double[_genomeSize];
    }

    public void Execute(
        int workerId,
        Span<double> population,
        Span<double> populationFfValues,
        Span<double> bufferPopulation,
        Span<double> bufferPopulationFfValues,
        out int bestHandledIndividualIndex)
    {
        var tempIndividual = MemoryMarshal.Cast<double, double>(_tempIndividual.Span);

        bestHandledIndividualIndex = workerId;
        for (var i = workerId; i < _populationSize; i += _individualHandlerStepSize)
        {
            _mutationStrategy.Mutate(i, population, tempIndividual);

            var tempIndividualFfValue = _fitnessFunctionEvaluator.Evaluate(tempIndividual);

            _selectionStrategy.Select(i, tempIndividualFfValue, tempIndividual, populationFfValues, population,
                                      bufferPopulationFfValues, bufferPopulation);

            if (bufferPopulationFfValues[i] < bufferPopulationFfValues[bestHandledIndividualIndex])
                bestHandledIndividualIndex = i;
        }
    }
}
