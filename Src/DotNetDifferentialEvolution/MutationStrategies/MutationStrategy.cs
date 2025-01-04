using System.Numerics;
using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies;

public class MutationStrategy : IMutationStrategy
{
    public const int NumberOfIndividualsToChoose = 3;
    
    private readonly double _mutationForce;
    private readonly double _crossoverProbability;
    
    private readonly int _populationSize;
    private readonly int _genomeSize;
    
    private readonly ReadOnlyMemory<double> _lowerBound;
    private readonly ReadOnlyMemory<double> _upperBound;
    
    private readonly BaseRandomProvider _randomProvider;
    
    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound,
        BaseRandomProvider randomProvider)
    {
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
        
        _populationSize = populationSize;
        _genomeSize = lowerBound.Length;
        
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        
        _randomProvider = randomProvider;
    }

    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        int populationSize,
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound)
    {
        var randomProvider = new RandomProvider();
        
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
        
        _populationSize = populationSize;
        _genomeSize = lowerBound.Length;
        
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        
        _randomProvider = randomProvider;
    }

    public void Mutate(
        int individualIndex,
        Span<double> population,
        Span<double> trialIndividual)
    {
#region ChoosingThreeRandomIndividuals

        Span<int> indexes = stackalloc int[NumberOfIndividualsToChoose];
        
        for (int i = 0; i < NumberOfIndividualsToChoose; i++)
        {
            var sizeWithoutCurrent = _populationSize - 1;
            indexes[i] = _randomProvider.Next(maxValue: sizeWithoutCurrent);
            if (indexes[i] >= individualIndex) indexes[i]++;

            for (int j = 0; j < i; j++)
            {
                if (indexes[i] == indexes[j])
                {
                    i--;
                    break;
                }
            }
        }

#endregion

#region CreatingTrialIndividual

        var firstIndividual = population.Slice(indexes[0] * _genomeSize, _genomeSize);
        var secondIndividual = population.Slice(indexes[1] * _genomeSize, _genomeSize);
        var thirdIndividual = population.Slice(indexes[2] * _genomeSize, _genomeSize);

        if (Vector<double>.Count <= _genomeSize)
        {
            var firstIndividualVectors = MemoryMarshal.Cast<double, Vector<double>>(firstIndividual);
            var secondIndividualVectors = MemoryMarshal.Cast<double, Vector<double>>(secondIndividual);
            var thirdIndividualVectors = MemoryMarshal.Cast<double, Vector<double>>(thirdIndividual);

            var trialIndividualVectors = MemoryMarshal.Cast<double, Vector<double>>(trialIndividual);
            for (int i = 0; i < trialIndividualVectors.Length; i++)
            {
                trialIndividualVectors[i] =
                    firstIndividualVectors[i] + _mutationForce * (secondIndividualVectors[i] - thirdIndividualVectors[i]);
            }
        }
        
        // Handling the remaining genes
        var handledGenesCount = _genomeSize - _genomeSize % Vector<double>.Count;
        for (int i = handledGenesCount; i < _genomeSize; i++)
        {
            trialIndividual[i] =
                firstIndividual[i] + _mutationForce * (secondIndividual[i] - thirdIndividual[i]);
        }

#endregion

#region CorrectingTrialIndividualGenes

        var lowerBound = _lowerBound.Span;
        var upperBound = _upperBound.Span;
        for (int i = 0; i < trialIndividual.Length; i++)
        {
            if (_randomProvider.NextDouble() <= _crossoverProbability)
            {
                if (trialIndividual[i] < lowerBound[i] || trialIndividual[i] > upperBound[i])
                {
                    trialIndividual[i] =
                        _randomProvider.NextDouble() * (upperBound[i] - lowerBound[i]) + lowerBound[i];
                }
            }
            else
            {
                trialIndividual[i] = population[individualIndex * _genomeSize + i];
            }
        }

#endregion
    }
}
