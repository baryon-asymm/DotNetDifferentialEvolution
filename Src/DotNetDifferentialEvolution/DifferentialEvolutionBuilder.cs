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
    
    private DifferentialEvolutionBuilder(
        IFitnessFunctionEvaluator fitnessFunctionEvaluator)
    {
        ArgumentNullException.ThrowIfNull(fitnessFunctionEvaluator);

        _fitnessFunctionEvaluator = fitnessFunctionEvaluator;
    }
    
    public static IBoundsRequired ForFunction(
        IFitnessFunctionEvaluator fitnessFunctionEvaluator)
    {
        return new DifferentialEvolutionBuilder(fitnessFunctionEvaluator);
    }
    
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

    public IPopulationSamplingRequired WithPopulationSize(
        int populationSize)
    {
        if (populationSize <= 0)
            throw new ArgumentException("Population size must be greater than 0.");
        
        _populationSize = populationSize;
        
        return this;
    }

    public IMutationStrategyRequired WithPopulationSampling(
        IPopulationSamplingMaker populationSamplingMaker)
    {
        ArgumentNullException.ThrowIfNull(populationSamplingMaker);
        
        _populationSamplingMaker = populationSamplingMaker;
        
        return this;
    }

    public IMutationStrategyRequired WithUniformPopulationSampling()
    {
        _populationSamplingMaker = new UniformRandomSamplingMaker(_lowerBound, _upperBound);
        
        return this;
    }

    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy)
    {
        ArgumentNullException.ThrowIfNull(mutationStrategy);
        
        _mutationStrategy = mutationStrategy;
        
        return this;
    }

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

    public ITerminationConditionRequired WithSelectionStrategy(
        ISelectionStrategy selectionStrategy)
    {
        ArgumentNullException.ThrowIfNull(selectionStrategy);
        
        _selectionStrategy = selectionStrategy;
        
        return this;
    }

    public ITerminationConditionRequired WithDefaultSelectionStrategy()
    {
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);
        
        return this;
    }

    public IWorkersCountRequired WithTerminationCondition(
        ITerminationStrategy terminationStrategy)
    {
        ArgumentNullException.ThrowIfNull(terminationStrategy);
        
        _terminationStrategy = terminationStrategy;
        
        return this;
    }

    public IDifferentialEvolutionBuilder WithWorkersCount(
        int workersCount)
    {
        if (workersCount <= 0)
            throw new ArgumentException("Workers count must be greater than 0.");
        
        _workersCount = workersCount;
        
        return this;
    }

    public IDifferentialEvolutionBuilder UseAllProcessors()
    {
        _workersCount = Environment.ProcessorCount;
        
        return this;
    }

    public IDifferentialEvolutionBuilder WithPopulationUpdateHandler(
        IPopulationUpdatedHandler populationUpdatedHandler)
    {
        _populationUpdatedHandler = populationUpdatedHandler;
        
        return this;
    }

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

public interface IBoundsRequired
{
    public IPopulationSizeRequired WithBounds(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound);
}

public interface IPopulationSizeRequired
{
    public IPopulationSamplingRequired WithPopulationSize(
        int populationSize);
}

public interface IPopulationSamplingRequired
{
    public IMutationStrategyRequired WithPopulationSampling(
        IPopulationSamplingMaker populationSamplingMaker);
    
    public IMutationStrategyRequired WithUniformPopulationSampling();
}

public interface IMutationStrategyRequired
{
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy);
    
    public ISelectionStrategyRequired WithDefaultMutationStrategy(
        double mutationForce,
        double crossoverProbability);
}

public interface ISelectionStrategyRequired
{
    public ITerminationConditionRequired WithSelectionStrategy(
        ISelectionStrategy selectionStrategy);
    
    public ITerminationConditionRequired WithDefaultSelectionStrategy();
}

public interface ITerminationConditionRequired
{
    public IWorkersCountRequired WithTerminationCondition(
        ITerminationStrategy terminationStrategy);
}

public interface IWorkersCountRequired
{
    public IDifferentialEvolutionBuilder WithWorkersCount(
        int workersCount);
    
    public IDifferentialEvolutionBuilder UseAllProcessors();
}

public interface IDifferentialEvolutionBuilder
{
    public IDifferentialEvolutionBuilder WithPopulationUpdateHandler(
        IPopulationUpdatedHandler populationUpdatedHandler);
    
    public DifferentialEvolution Build();
}
