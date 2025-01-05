using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.PopulationSamplingMaker;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;

namespace DotNetDifferentialEvolution;

/// <summary>
/// Provides a builder for creating instances of the <see cref="DifferentialEvolution"/> class.
/// </summary>
public class DifferentialEvolutionBuilder 
    : IBoundsRequired,
      IPopulationSizeRequired,
      IPopulationSamplingRequired,
      IMutationStrategyRequired,
      ISelectionStrategyRequired,
      ITerminationConditionRequired,
      IWorkersCountRequired,
      IDifferentialEvolutionBuilder
{
    private readonly IFitnessFunctionEvaluator _fitnessFunctionEvaluator;
    
    private ReadOnlyMemory<double> _lowerBound;
    private ReadOnlyMemory<double> _upperBound;
    
    private int _populationSize;
    
    private IPopulationSamplingMaker? _populationSamplingMaker;
    
    private IMutationStrategy? _mutationStrategy;
    private ISelectionStrategy? _selectionStrategy;
    private ITerminationStrategy? _terminationStrategy;
    
    private int _workersCount;
    
    private IPopulationUpdatedHandler? _populationUpdatedHandler;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DifferentialEvolutionBuilder"/> class.
    /// </summary>
    /// <param name="fitnessFunctionEvaluator">The evaluator for the fitness function.</param>
    private DifferentialEvolutionBuilder(
        IFitnessFunctionEvaluator fitnessFunctionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunctionEvaluator);

        _fitnessFunctionEvaluator = fitnessFunctionEvaluator;
    }
    
    /// <summary>
    /// Creates a new builder for the specified fitness function evaluator.
    /// </summary>
    /// <param name="fitnessFunctionEvaluator">The evaluator for the fitness function.</param>
    /// <returns>An instance of <see cref="IBoundsRequired"/> to set the bounds.</returns>
    public static IBoundsRequired ForFunction(
        IFitnessFunctionEvaluator fitnessFunctionEvaluator)
    {
        return new DifferentialEvolutionBuilder(fitnessFunctionEvaluator);
    }
    
    /// <summary>
    /// Sets the bounds for the population.
    /// </summary>
    /// <param name="lowerBound">The lower bound of the population.</param>
    /// <param name="upperBound">The upper bound of the population.</param>
    /// <returns>An instance of <see cref="IPopulationSizeRequired"/> to set the population size.</returns>
    public IPopulationSizeRequired WithBounds(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound)
    {
        ArgumentNullException.ThrowIfNull(lowerBound);
        ArgumentNullException.ThrowIfNull(upperBound);
        
        if (lowerBound.Length != upperBound.Length)
            throw new ArgumentException("Lower and upper bounds must have the same length.");

        for (int i = 0; i < lowerBound.Length; i++)
        {
            if (lowerBound.Span[i] > upperBound.Span[i])
                throw new ArgumentException("Lower bound must be less than or equal upper bound.");
        }
        
        _lowerBound = lowerBound;
        _upperBound = upperBound;
        
        return this;
    }

    /// <summary>
    /// Sets the population size.
    /// </summary>
    /// <param name="populationSize">The size of the population.</param>
    /// <returns>An instance of <see cref="IPopulationSamplingRequired"/> to set the population sampling method.</returns>
    public IPopulationSamplingRequired WithPopulationSize(
        int populationSize)
    {
        if (populationSize <= 0)
            throw new ArgumentException("Population size must be greater than 0.");
        
        _populationSize = populationSize;
        
        return this;
    }

    /// <summary>
    /// Sets the population sampling method.
    /// </summary>
    /// <param name="populationSamplingMaker">The population sampling maker.</param>
    /// <returns>An instance of <see cref="IMutationStrategyRequired"/> to set the mutation strategy.</returns>
    public IMutationStrategyRequired WithPopulationSampling(
        IPopulationSamplingMaker populationSamplingMaker)
    {
        ArgumentNullException.ThrowIfNull(populationSamplingMaker);
        
        _populationSamplingMaker = populationSamplingMaker;
        
        return this;
    }

    /// <summary>
    /// Sets the population sampling method to uniform random sampling.
    /// </summary>
    /// <returns>An instance of <see cref="IMutationStrategyRequired"/> to set the mutation strategy.</returns>
    public IMutationStrategyRequired WithUniformPopulationSampling()
    {
        _populationSamplingMaker = new UniformRandomSamplingMaker(_lowerBound, _upperBound);
        
        return this;
    }

    /// <summary>
    /// Sets the mutation strategy.
    /// </summary>
    /// <param name="mutationStrategy">The mutation strategy.</param>
    /// <returns>An instance of <see cref="ISelectionStrategyRequired"/> to set the selection strategy.</returns>
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy)
    {
        ArgumentNullException.ThrowIfNull(mutationStrategy);
        
        _mutationStrategy = mutationStrategy;
        
        return this;
    }

    /// <summary>
    /// Sets the default mutation strategy.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <returns>An instance of <see cref="ISelectionStrategyRequired"/> to set the selection strategy.</returns>
    public ISelectionStrategyRequired WithDefaultMutationStrategy(
        double mutationForce,
        double crossoverProbability)
    {
        _mutationStrategy = new MutationStrategy(
            mutationForce: mutationForce,
            crossoverProbability: crossoverProbability,
            populationSize: _populationSize,
            lowerBound: _lowerBound,
            upperBound: _upperBound);
        
        return this;
    }

    /// <summary>
    /// Sets the selection strategy.
    /// </summary>
    /// <param name="selectionStrategy">The selection strategy.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithSelectionStrategy(
        ISelectionStrategy selectionStrategy)
    {
        ArgumentNullException.ThrowIfNull(selectionStrategy);
        
        _selectionStrategy = selectionStrategy;
        
        return this;
    }

    /// <summary>
    /// Sets the default selection strategy.
    /// </summary>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithDefaultSelectionStrategy()
    {
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);
        
        return this;
    }

    /// <summary>
    /// Sets the termination condition.
    /// </summary>
    /// <param name="terminationStrategy">The termination strategy.</param>
    /// <returns>An instance of <see cref="IWorkersCountRequired"/> to set the number of workers.</returns>
    public IWorkersCountRequired WithTerminationCondition(
        ITerminationStrategy terminationStrategy)
    {
        ArgumentNullException.ThrowIfNull(terminationStrategy);
        
        _terminationStrategy = terminationStrategy;
        
        return this;
    }

    /// <summary>
    /// Sets the number of processors to use.
    /// </summary>
    /// <param name="processorsCount">The number of processors.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder UseProcessors(
        int processorsCount)
    {
        if (processorsCount <= 0)
            throw new ArgumentException("Processors count must be greater than 0.");
        
        _workersCount = processorsCount;
        
        return this;
    }

    /// <summary>
    /// Sets the number of processors to use to the total number of available processors.
    /// </summary>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder UseAllProcessors()
    {
        _workersCount = Environment.ProcessorCount;
        
        return this;
    }

    /// <summary>
    /// Sets the population update handler.
    /// </summary>
    /// <param name="populationUpdatedHandler">The population update handler.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder WithPopulationUpdateHandler(
        IPopulationUpdatedHandler populationUpdatedHandler)
    {
        _populationUpdatedHandler = populationUpdatedHandler;
        
        return this;
    }

    /// <summary>
    /// Builds the Differential Evolution instance.
    /// </summary>
    /// <returns>An instance of <see cref="DifferentialEvolution"/>.</returns>
    public DifferentialEvolution Build()
    {
        EnsureReadyStateToBuild();
        
        var genomeSize = _lowerBound.Length;
        
        var population = new double[_populationSize * genomeSize];
        var populationFfValues = new double[_populationSize];
        var trialPopulation = new double[_populationSize * genomeSize];
        var trialPopulationFfValues = new double[_populationSize];
        
        _populationSamplingMaker!.SamplePopulation(population);

        EvaluatePopulationFfValues(
            population,
            populationFfValues);
        
        var context = new ProblemContext(
            populationSize: _populationSize,
            genomeSize: genomeSize,
            workersCount: _workersCount,
            genesLowerBound: _lowerBound,
            genesUpperBound: _upperBound,
            fitnessFunctionEvaluator: _fitnessFunctionEvaluator,
            terminationStrategy: _terminationStrategy!,
            population: population,
            populationFfValues: populationFfValues,
            trialPopulation: trialPopulation,
            trialPopulationFfValues: trialPopulationFfValues)
        {
            PopulationUpdatedHandler = _populationUpdatedHandler
        };
        
        var algorithmExecutor = new AlgorithmExecutor(
            _mutationStrategy!,
            _selectionStrategy!,
            context);
        
        return new DifferentialEvolution(context, algorithmExecutor);
    }

    /// <summary>
    /// Ensures that all required parameters are set before building the Differential Evolution instance.
    /// </summary>
    private void EnsureReadyStateToBuild()
    {
        if (_lowerBound.Length == 0)
            throw new InvalidOperationException("Lower bound must be set.");
        
        if (_upperBound.Length == 0)
            throw new InvalidOperationException("Upper bound must be set.");
        
        if (_populationSize == 0)
            throw new InvalidOperationException("Population size must be set.");
        
        if (_populationSamplingMaker == null)
            throw new InvalidOperationException("Population sampling maker must be set.");
        
        if (_mutationStrategy == null)
            throw new InvalidOperationException("Mutation strategy must be set.");
        
        if (_selectionStrategy == null)
            throw new InvalidOperationException("Selection strategy must be set.");
        
        if (_terminationStrategy == null)
            throw new InvalidOperationException("Termination strategy must be set.");
        
        if (_workersCount == 0)
            throw new InvalidOperationException("Workers count must be set.");
    }
    
    /// <summary>
    /// Evaluates the fitness function values for the population.
    /// </summary>
    /// <param name="population">The population of individuals.</param>
    /// <param name="populationFfValues">The fitness function values of the population.</param>
    private void EvaluatePopulationFfValues(
        ReadOnlySpan<double> population,
        Span<double> populationFfValues)
    {
        var genomeSize = _lowerBound.Length;
        
        for (int i = 0; i < populationFfValues.Length; i++)
        {
            var individual = population.Slice(i * genomeSize, genomeSize);
            
            populationFfValues[i] = _fitnessFunctionEvaluator.Evaluate(individual);
        }
    }
}

/// <summary>
/// Interface for setting the bounds in the Differential Evolution builder.
/// </summary>
public interface IBoundsRequired
{
    /// <summary>
    /// Sets the bounds for the population.
    /// </summary>
    /// <param name="lowerBound">The lower bound of the population.</param>
    /// <param name="upperBound">The upper bound of the population.</param>
    /// <returns>An instance of <see cref="IPopulationSizeRequired"/> to set the population size.</returns>
    public IPopulationSizeRequired WithBounds(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound);
}

/// <summary>
/// Interface for setting the population size in the Differential Evolution builder.
/// </summary>
public interface IPopulationSizeRequired
{
    /// <summary>
    /// Sets the population size.
    /// </summary>
    /// <param name="populationSize">The size of the population.</param>
    /// <returns>An instance of <see cref="IPopulationSamplingRequired"/> to set the population sampling method.</returns>
    public IPopulationSamplingRequired WithPopulationSize(
        int populationSize);
}

/// <summary>
/// Interface for setting the population sampling method in the Differential Evolution builder.
/// </summary>
public interface IPopulationSamplingRequired
{
    /// <summary>
    /// Sets the population sampling method.
    /// </summary>
    /// <param name="populationSamplingMaker">The population sampling maker.</param>
    /// <returns>An instance of <see cref="IMutationStrategyRequired"/> to set the mutation strategy.</returns>
    public IMutationStrategyRequired WithPopulationSampling(
        IPopulationSamplingMaker populationSamplingMaker);
    
    /// <summary>
    /// Sets the population sampling method to uniform random sampling.
    /// </summary>
    /// <returns>An instance of <see cref="IMutationStrategyRequired"/> to set the mutation strategy.</returns>
    public IMutationStrategyRequired WithUniformPopulationSampling();
}

/// <summary>
/// Interface for setting the mutation strategy in the Differential Evolution builder.
/// </summary>
public interface IMutationStrategyRequired
{
    /// <summary>
    /// Sets the mutation strategy.
    /// </summary>
    /// <param name="mutationStrategy">The mutation strategy.</param>
    /// <returns>An instance of <see cref="ISelectionStrategyRequired"/> to set the selection strategy.</returns>
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy);
    
    /// <summary>
    /// Sets the default mutation strategy.
    /// </summary>
    /// <param name="mutationForce">The mutation force.</param>
    /// <param name="crossoverProbability">The crossover probability.</param>
    /// <returns>An instance of <see cref="ISelectionStrategyRequired"/> to set the selection strategy.</returns>
    public ISelectionStrategyRequired WithDefaultMutationStrategy(
        double mutationForce,
        double crossoverProbability);
}

/// <summary>
/// Interface for setting the selection strategy in the Differential Evolution builder.
/// </summary>
public interface ISelectionStrategyRequired
{
    /// <summary>
    /// Sets the selection strategy.
    /// </summary>
    /// <param name="selectionStrategy">The selection strategy.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithSelectionStrategy(
        ISelectionStrategy selectionStrategy);
    
    /// <summary>
    /// Sets the default selection strategy.
    /// </summary>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithDefaultSelectionStrategy();
}

/// <summary>
/// Interface for setting the termination condition in the Differential Evolution builder.
/// </summary>
public interface ITerminationConditionRequired
{
    /// <summary>
    /// Sets the termination condition.
    /// </summary>
    /// <param name="terminationStrategy">The termination strategy.</param>
    /// <returns>An instance of <see cref="IWorkersCountRequired"/> to set the number of workers.</returns>
    public IWorkersCountRequired WithTerminationCondition(
        ITerminationStrategy terminationStrategy);
}

/// <summary>
/// Interface for setting the number of workers in the Differential Evolution builder.
/// </summary>
public interface IWorkersCountRequired
{
    /// <summary>
    /// Sets the number of processors to use.
    /// </summary>
    /// <param name="processorsCount">The number of processors.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder UseProcessors(
        int processorsCount);
    
    /// <summary>
    /// Sets the number of processors to use to the total number of available processors.
    /// </summary>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder UseAllProcessors();
}

/// <summary>
/// Interface for building the Differential Evolution instance.
/// </summary>
public interface IDifferentialEvolutionBuilder
{
    /// <summary>
    /// Sets the population update handler.
    /// </summary>
    /// <param name="populationUpdatedHandler">The population update handler.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder WithPopulationUpdateHandler(
        IPopulationUpdatedHandler populationUpdatedHandler);
    
    /// <summary>
    /// Builds the Differential Evolution instance.
    /// </summary>
    /// <returns>An instance of <see cref="DifferentialEvolution"/>.</returns>
    public DifferentialEvolution Build();
}
