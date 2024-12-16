using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomGenerators.Interfaces;

namespace DotNetDifferentialEvolution.MutationStrategies;

public class MutationStrategy : IMutationStrategy
{
    public const int NumberOfIndividualsToChoose = 3;

    private readonly int _populationSize;
    private readonly int _genomeSize;

    private readonly double _mutationForce;
    private readonly double _crossoverProbability;

    private readonly ReadOnlyMemory<double> _lowerBound;
    private readonly ReadOnlyMemory<double> _upperBound;

    private readonly IRandomGenerator _randomGenerator;

    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        IRandomGenerator randomGenerator,
        DEContext context)
    {
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;

        _lowerBound = context.GenesLowerBound;
        _upperBound = context.GenesUpperBound;

        _populationSize = context.PopulationSize;
        _genomeSize = context.GenomeSize;
        _randomGenerator = randomGenerator;
    }

    public void Mutate(
        int individualIndex,
        Span<double> population,
        Span<double> bufferPopulation)
    {
        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];

        var lowerBound = MemoryMarshal.Cast<double, double>(_lowerBound.Span);
        var upperBound = MemoryMarshal.Cast<double, double>(_upperBound.Span);

        for (var i = 0; i < NumberOfIndividualsToChoose; i++)
        {
            var sizeWithoutCurrent = _populationSize - 1;
            indexes[i] = _randomGenerator.NextInt(individualIndex) % sizeWithoutCurrent;
            if (indexes[i] >= individualIndex) indexes[i]++;

            for (var j = 0; j < i; j++)
                if (indexes[i] == indexes[j])
                {
                    i--;
                    break;
                }
        }

        for (var i = 0; i < _genomeSize; i++)
        {
            if (_randomGenerator.NextDouble(individualIndex) <= _crossoverProbability)
            {
                var mutatedGene = population[indexes[0] * _genomeSize + i]
                                  + _mutationForce * (population[indexes[1] * _genomeSize + i]
                                                      - population[indexes[2] * _genomeSize + i]);
                if (mutatedGene >= lowerBound[i] && mutatedGene <= upperBound[i])
                {
                    bufferPopulation[individualIndex * _genomeSize + i] = mutatedGene;
                }
                else
                {
                    bufferPopulation[individualIndex * _genomeSize + i] = _randomGenerator.NextDouble(individualIndex)
                        * (upperBound[i] - lowerBound[i]) + lowerBound[i];
                }
            }
            else
            {
                bufferPopulation[individualIndex * _genomeSize + i] = population[individualIndex * _genomeSize + i];
            }
        }
    }
}