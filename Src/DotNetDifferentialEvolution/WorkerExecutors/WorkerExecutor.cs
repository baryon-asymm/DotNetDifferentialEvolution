using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.WorkerExecutors.Interfaces;

namespace DotNetDifferentialEvolution.WorkerExecutors;

public class WorkerExecutor : IWorkerExecutor
{
    private readonly int _populationSize;
    private readonly int _genomeSize;

    private readonly int _individualHandlerStepSize;

    private readonly Memory<double> _populationFfValues;
    private readonly Memory<double> _population;

    private readonly Memory<double> _bufferPopulationFfFfValues;
    private readonly Memory<double> _bufferPopulation;

    private readonly IMutationStrategy _mutationStrategy;

    private readonly IFitnessFunctionEvaluator _fitnessFunctionEvaluator;

    public WorkerExecutor(
        Memory<double> populationFfValues,
        Memory<double> population,
        Memory<double> bufferPopulationFfValues,
        Memory<double> bufferPopulation,
        IMutationStrategy mutationStrategy,
        DEContext context)
    {
        _populationSize = context.PopulationSize;
        _genomeSize = context.GenomeSize;

        _individualHandlerStepSize = context.WorkersCount;

        _populationFfValues = populationFfValues;
        _population = population;

        _bufferPopulationFfFfValues = bufferPopulationFfValues;
        _bufferPopulation = bufferPopulation;

        _mutationStrategy = mutationStrategy;

        _fitnessFunctionEvaluator = context.FitnessFunctionEvaluator;
    }

    public void Execute(int workerId)
    {
        var population = MemoryMarshal.Cast<double, double>(_population.Span);
        var bufferPopulation = MemoryMarshal.Cast<double, double>(_bufferPopulation.Span);

        var bufferPopulationFfValues = MemoryMarshal.Cast<double, double>(_bufferPopulationFfFfValues.Span);

        for (int i = workerId; i < _populationSize; i += _individualHandlerStepSize)
        {
            _mutationStrategy.Mutate(i, population, bufferPopulation);
            
            ReadOnlySpan<double> genes = population.Slice(i * _genomeSize, _genomeSize);
            bufferPopulationFfValues[i] = _fitnessFunctionEvaluator.Evaluate(genes);
        }
    }
}