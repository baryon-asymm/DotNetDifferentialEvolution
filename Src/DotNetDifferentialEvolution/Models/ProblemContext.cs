using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.Models;

/// <summary>
/// Represents the context of a problem to be solved using Differential Evolution.
/// </summary>
public class ProblemContext
{
    private Population _population;
    private Population _trialPopulation;
    
    /// <summary>
    /// Gets the size of the population.
    /// </summary>
    public int PopulationSize { get; init; }

    /// <summary>
    /// Gets the size of the genome.
    /// </summary>
    public int GenomeSize { get; init; }

    /// <summary>
    /// Gets the number of workers.
    /// </summary>
    public int WorkersCount { get; init; }

    /// <summary>
    /// Gets the lower bound of the genes.
    /// </summary>
    public ReadOnlyMemory<double> GenesLowerBound { get; init; }

    /// <summary>
    /// Gets the upper bound of the genes.
    /// </summary>
    public ReadOnlyMemory<double> GenesUpperBound { get; init; }

    /// <summary>
    /// Gets the fitness function evaluator.
    /// </summary>
    public IFitnessFunctionEvaluator FitnessFunctionEvaluator { get; init; }
    
    /// <summary>
    /// Gets the termination strategy.
    /// </summary>
    public ITerminationStrategy TerminationStrategy { get; init; }
    
    /// <summary>
    /// Gets the handler for population updates.
    /// </summary>
    public IPopulationUpdatedHandler? PopulationUpdatedHandler { get; init; }

    /// <summary>
    /// Gets the current population.
    /// </summary>
    public Memory<double> Population { get; private set; }

    /// <summary>
    /// Gets the fitness function values of the current population.
    /// </summary>
    public Memory<double> PopulationFfValues { get; private set; }

    /// <summary>
    /// Gets the trial population.
    /// </summary>
    public Memory<double> TrialPopulation { get; private set; }

    /// <summary>
    /// Gets the fitness function values of the trial population.
    /// </summary>
    public Memory<double> TrialPopulationFfValues { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProblemContext"/> class.
    /// </summary>
    /// <param name="populationSize">The size of the population.</param>
    /// <param name="genomeSize">The size of the genome.</param>
    /// <param name="workersCount">The number of workers.</param>
    /// <param name="genesLowerBound">The lower bound of the genes.</param>
    /// <param name="genesUpperBound">The upper bound of the genes.</param>
    /// <param name="fitnessFunctionEvaluator">The fitness function evaluator.</param>
    /// <param name="terminationStrategy">The termination strategy.</param>
    /// <param name="population">The current population.</param>
    /// <param name="populationFfValues">The fitness function values of the current population.</param>
    /// <param name="trialPopulation">The trial population.</param>
    /// <param name="trialPopulationFfValues">The fitness function values of the trial population.</param>
    public ProblemContext(
        int populationSize,
        int genomeSize,
        int workersCount,
        ReadOnlyMemory<double> genesLowerBound,
        ReadOnlyMemory<double> genesUpperBound,
        IFitnessFunctionEvaluator fitnessFunctionEvaluator,
        ITerminationStrategy terminationStrategy,
        Memory<double> population,
        Memory<double> populationFfValues,
        Memory<double> trialPopulation,
        Memory<double> trialPopulationFfValues)
    {
        PopulationSize = populationSize;
        GenomeSize = genomeSize;
        WorkersCount = workersCount;
        GenesLowerBound = genesLowerBound;
        GenesUpperBound = genesUpperBound;
        FitnessFunctionEvaluator = fitnessFunctionEvaluator;
        TerminationStrategy = terminationStrategy;
        Population = population;
        PopulationFfValues = populationFfValues;
        TrialPopulation = trialPopulation;
        TrialPopulationFfValues = trialPopulationFfValues;
        
        _population = new Population(
            population,
            populationFfValues);
        
        _trialPopulation = new Population(
            trialPopulation,
            trialPopulationFfValues);
    }

    /// <summary>
    /// Swaps the current population with the trial population.
    /// </summary>
    public void SwapPopulations()
    {
        (Population, TrialPopulation) = (TrialPopulation, Population);

        (PopulationFfValues, TrialPopulationFfValues) = (TrialPopulationFfValues, PopulationFfValues);

        (_population, _trialPopulation) = (_trialPopulation, _population);
    }
    
    /// <summary>
    /// Gets the representative population for a given generation.
    /// </summary>
    /// <param name="generationNumber">The generation number.</param>
    /// <param name="bestIndividualIndex">The index of the best individual in the population.</param>
    /// <returns>The representative population.</returns>
    public Population GetRepresentativePopulation(
        int generationNumber,
        int bestIndividualIndex)
    {
        _population.GenerationNumber = generationNumber;
        _population.BestIndividualIndex = bestIndividualIndex;
        
        return _population;
    }
}
