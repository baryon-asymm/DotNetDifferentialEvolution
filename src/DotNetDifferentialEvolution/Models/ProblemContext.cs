using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.LocalSearch;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
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
    /// Gets the provider of per-individual control parameters (F and CR).
    /// When <see langword="null"/>, mutation strategies fall back to their own fixed parameters.
    /// </summary>
    public IControlParameterProvider? ControlParameterProvider { get; init; }

    /// <summary>
    /// Gets the per-generation adaptation hook. When <see langword="null"/>, no adaptation is performed.
    /// </summary>
    public IGenerationStrategy? GenerationStrategy { get; init; }

    /// <summary>
    /// Gets the seed making the run reproducible, or <see langword="null"/> for a run that is
    /// free to differ. Each worker draws from its own generator derived from this seed, so a
    /// seeded run reproduces exactly at a given worker count.
    /// </summary>
    public int? RandomSeed { get; init; }

    /// <summary>
    /// Gets what the configured mutation strategy needs the engine to provision (see
    /// <see cref="IMutationStrategy.Requirements"/>). The engine acts on this: a declared
    /// <see cref="MutationRequirements.FitnessRanking"/> makes it re-rank the active population
    /// every generation, and a declared <see cref="MutationRequirements.ControlParameters"/> is
    /// what the builder validates <see cref="ControlParameterProvider"/> against.
    /// </summary>
    public MutationRequirements MutationRequirements { get; init; }

    /// <summary>
    /// Gets the optional local-search refiner invoked every <see cref="LocalSearchInterval"/>
    /// generations to polish the population in place. When <see langword="null"/>, no local search runs.
    /// </summary>
    public ILocalSearchRefiner? LocalSearchRefiner { get; init; }

    /// <summary>
    /// Gets the generation interval at which <see cref="LocalSearchRefiner"/> runs (1 = every generation).
    /// </summary>
    public int LocalSearchInterval { get; init; } = 1;

    /// <summary>
    /// Gets the per-individual trial outcomes for the current generation. Workers write
    /// disjoint indices; the generation strategy reads the aggregated buffer.
    /// </summary>
    public Memory<TrialRecord> TrialRecords { get; private set; }

    /// <summary>
    /// Gets or sets the number of active individuals in the population. This equals
    /// <see cref="PopulationSize"/> unless a strategy (e.g. L-SHADE) reduces it.
    /// </summary>
    public int CurrentPopulationSize { get; set; }

    /// <summary>
    /// Gets or sets the index of the best individual in the current population, refreshed
    /// each generation so the next generation's mutation can reference it.
    /// </summary>
    public int BestIndividualIndex { get; set; }

    /// <summary>
    /// Gets or sets the total number of fitness-function evaluations performed so far.
    /// Used by L-SHADE's population-size reduction and by evaluation-budget termination.
    /// </summary>
    public long EvaluationCount { get; set; }

    /// <summary>
    /// Gets or sets the flattened genes of the external archive (length up to capacity * genome size).
    /// </summary>
    public Memory<double> Archive { get; set; }

    /// <summary>
    /// Gets or sets the number of individuals currently stored in <see cref="Archive"/>.
    /// </summary>
    public int ArchiveSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of individuals the archive may hold. L-SHADE reduces
    /// this together with the population size; it never exceeds the allocated buffer.
    /// </summary>
    public int ArchiveCapacity { get; set; }

    /// <summary>
    /// Gets the population indices sorted ascending by fitness (best first), maintained by
    /// p-best strategies for the upcoming generation. Empty until populated.
    /// </summary>
    public Memory<int> FitnessSortedIndices { get; private set; }

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

        CurrentPopulationSize = populationSize;
        TrialRecords = new TrialRecord[populationSize];
        FitnessSortedIndices = new int[populationSize];

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
        _population.EvaluationCount = EvaluationCount;
        _population.PopulationSize = CurrentPopulationSize;

        return _population;
    }
}
