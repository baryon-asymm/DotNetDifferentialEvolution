using System.Numerics;
using System.Runtime.InteropServices;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;

namespace DotNetDifferentialEvolution.MutationStrategies;

/// <summary>
/// Represents a mutation strategy for Differential Evolution.
/// </summary>
public class MutationStrategy : IMutationStrategy
{
    /// <summary>
    /// The number of individuals to choose for mutation.
    /// </summary>
    public const int NumberOfIndividualsToChoose = 3;
    
    private readonly double _mutationForce;
    private readonly double _crossoverProbability;
    
    private readonly int _populationSize;
    private readonly int _genomeSize;
    
    private readonly ReadOnlyMemory<double> _lowerBound;
    private readonly ReadOnlyMemory<double> _upperBound;
    
    private readonly BaseRandomProvider _randomProvider;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MutationStrategy"/> class.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <param name="populationSize">The size of the population.</param>
    /// <param name="lowerBound">The lower bound of the genes.</param>
    /// <param name="upperBound">The upper bound of the genes.</param>
    /// <param name="randomProvider">The random provider.</param>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MutationStrategy"/> class with a default random provider.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <param name="populationSize">The size of the population.</param>
    /// <param name="lowerBound">The lower bound of the genes.</param>
    /// <param name="upperBound">The upper bound of the genes.</param>
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

    /// <summary>
    /// Mutates an individual in the population.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to mutate.</param>
    /// <param name="population">The population of individuals.</param>
    /// <param name="trialIndividual">The trial individual to be mutated.</param>
    public void Mutate(
        int individualIndex,
        Span<double> population,
        Span<double> trialIndividual)
    {
#region ChoosingThreeRandomIndividuals

        // Choose three random individuals from the population
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

        // Create the trial individual by combining the chosen individuals
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

        // Correct the trial individual genes to ensure they are within bounds
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
