using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.Models;
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
    
    private readonly RandomProvider _randomProvider;
    
    private readonly Memory<double> _randomValuesVector;
    
    public MutationStrategy(
        double mutationForce,
        double crossoverProbability,
        RandomProvider randomProvider,
        DEContext context)
    {
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
        
        _populationSize = context.PopulationSize;
        _genomeSize = context.GenomeSize;
        
        _lowerBound = context.GenesLowerBound;
        _upperBound = context.GenesUpperBound;
        
        _randomProvider = randomProvider;
        
        _randomValuesVector = new double[Vector<double>.Count];
        for (int i = 0; i < _randomValuesVector.Length; i++)
        {
            _randomValuesVector.Span[i] = _randomProvider.NextDouble();
        }
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

        var firstIndividual = MemoryMarshal.Cast<double, Vector<double>>(
            population.Slice(indexes[0] * _genomeSize, _genomeSize));
        var secondIndividual = MemoryMarshal.Cast<double, Vector<double>>(
            population.Slice(indexes[1] * _genomeSize, _genomeSize));
        var thirdIndividual = MemoryMarshal.Cast<double, Vector<double>>(
            population.Slice(indexes[2] * _genomeSize, _genomeSize));

        var trialIndividualVectors = MemoryMarshal.Cast<double, Vector<double>>(trialIndividual);
        for (int i = 0; i < trialIndividualVectors.Length; i++)
        {
            trialIndividualVectors[i] =
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
        
        /*var lowerBound = MemoryMarshal.Cast<double, Vector<double>>(_lowerBound.Span);
        var upperBound = MemoryMarshal.Cast<double, Vector<double>>(_upperBound.Span);
        
        var randomValuesVector = MemoryMarshal.Cast<double, Vector<double>>(_randomValuesVector.Span)[0];

        var populationVectors =
            MemoryMarshal.Cast<double, Vector<double>>(population.Slice(individualIndex * _genomeSize, _genomeSize));
        
        var crossoverProbability = new Vector<double>(_crossoverProbability);
        
        Unsafe.SkipInit(out Vector<double> inboundsGenes);
        Unsafe.SkipInit(out Vector<long> maskLessThanLowerBound);
        Unsafe.SkipInit(out Vector<long> maskGreaterThanUpperBound);
        Unsafe.SkipInit(out Vector<long> maskCrossover);
        for (int i = 0; i < lowerBound.Length; i++)
        {
            inboundsGenes = randomValuesVector * (upperBound[i] - lowerBound[i]) + lowerBound[i];

            randomValuesVector = _randomProvider.NextVectorByXorShift64(randomValuesVector);
            
            maskLessThanLowerBound = Vector.LessThan(trialIndividualVectors[i], lowerBound[i]);
            maskGreaterThanUpperBound = Vector.GreaterThan(trialIndividualVectors[i], upperBound[i]);

            trialIndividualVectors[i] =
                Vector.ConditionalSelect(maskLessThanLowerBound, inboundsGenes, trialIndividualVectors[i]);
            trialIndividualVectors[i] =
                Vector.ConditionalSelect(maskGreaterThanUpperBound, inboundsGenes, trialIndividualVectors[i]);
            
            maskCrossover = Vector.GreaterThanOrEqual(randomValuesVector, crossoverProbability);
            trialIndividualVectors[i] =
                Vector.ConditionalSelect(maskCrossover, populationVectors[i], trialIndividualVectors[i]);
            
            randomValuesVector = _randomProvider.NextVectorByXorShift64(randomValuesVector);
        }

        randomValuesVector.StoreUnsafe(ref _randomValuesVector.Span[0]);*/

#endregion
    }
}
