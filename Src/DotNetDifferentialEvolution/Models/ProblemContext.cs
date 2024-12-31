using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution.Models;

public class ProblemContext
{
    private Population _population;
    private Population _trialPopulation;
    
    public int PopulationSize { get; init; }

    public int GenomeSize { get; init; }

    public int WorkersCount { get; init; }

    public ReadOnlyMemory<double> GenesLowerBound { get; init; }

    public ReadOnlyMemory<double> GenesUpperBound { get; init; }

    public IFitnessFunctionEvaluator FitnessFunctionEvaluator { get; init; }
    
    public ITerminationStrategy TerminationStrategy { get; init; }
    
    public IPopulationUpdatedHandler? PopulationUpdatedHandler { get; init; }

    public Memory<double> Population { get; private set; }

    public Memory<double> PopulationFfValues { get; private set; }

    public Memory<double> TrialPopulation { get; private set; }

    public Memory<double> TrialPopulationFfValues { get; private set; }

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

    public void SwapPopulations()
    {
        (Population, TrialPopulation) = (TrialPopulation, Population);

        (PopulationFfValues, TrialPopulationFfValues) = (TrialPopulationFfValues, PopulationFfValues);

        (_population, _trialPopulation) = (_trialPopulation, _population);
    }
    
    public Population GetRepresentativePopulation(
        int generationNumber,
        int bestIndividualIndex)
    {
        _population.GenerationNumber = generationNumber;
        _population.BestIndividualIndex = bestIndividualIndex;
        
        return _population;
    }
}
