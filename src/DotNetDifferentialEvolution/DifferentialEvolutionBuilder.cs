using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.PopulationSamplingMaker;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
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

    private IControlParameterProvider? _controlParameterProvider;
    private IGenerationStrategy? _generationStrategy;

    private int _archiveCapacity;

    private long? _lShadeMaxEvaluationNumber;

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
    /// Sets the <c>DE/best/1/bin</c> mutation strategy with constant parameters.
    /// </summary>
    /// <param name="mutationForce">The mutation force (F).</param>
    /// <param name="crossoverProbability">The crossover probability (CR).</param>
    public ISelectionStrategyRequired WithBestMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new BestMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <summary>
    /// Sets the <c>DE/current-to-best/1/bin</c> mutation strategy with constant parameters.
    /// </summary>
    /// <param name="mutationForce">The mutation force (F).</param>
    /// <param name="crossoverProbability">The crossover probability (CR).</param>
    public ISelectionStrategyRequired WithCurrentToBestMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new CurrentToBestMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <summary>
    /// Sets the <c>DE/rand/2/bin</c> mutation strategy with constant parameters.
    /// </summary>
    /// <param name="mutationForce">The mutation force (F).</param>
    /// <param name="crossoverProbability">The crossover probability (CR).</param>
    public ISelectionStrategyRequired WithRandTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new RandTwoMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <summary>
    /// Sets the <c>DE/best/2/bin</c> mutation strategy with constant parameters.
    /// </summary>
    /// <param name="mutationForce">The mutation force (F).</param>
    /// <param name="crossoverProbability">The crossover probability (CR).</param>
    public ISelectionStrategyRequired WithBestTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new BestTwoMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <summary>
    /// Sets the mutation strategy together with the control-parameter provider that
    /// supplies its per-individual F and CR (e.g. <see cref="ConstantControlParameterProvider"/>
    /// or <see cref="DitheredControlParameterProvider"/>).
    /// </summary>
    /// <param name="mutationStrategy">The mutation strategy.</param>
    /// <param name="controlParameterProvider">The control-parameter provider.</param>
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy,
        IControlParameterProvider controlParameterProvider)
    {
        ArgumentNullException.ThrowIfNull(mutationStrategy);
        ArgumentNullException.ThrowIfNull(controlParameterProvider);

        _mutationStrategy = mutationStrategy;
        _controlParameterProvider = controlParameterProvider;

        return this;
    }

    /// <summary>
    /// Configures the self-adaptive jDE algorithm (Brest et al., 2006): <c>DE/rand/1/bin</c>
    /// with per-individual self-adapting F and CR and greedy selection. This bundles the
    /// mutation, control-parameter, generation, and selection strategies.
    /// </summary>
    /// <param name="initialMutationForce">The initial mutation factor for every individual.</param>
    /// <param name="initialCrossoverProbability">The initial crossover probability for every individual.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithJde(
        double initialMutationForce = JdeStrategy.DefaultInitialMutationForce,
        double initialCrossoverProbability = JdeStrategy.DefaultInitialCrossoverProbability)
    {
        var jdeStrategy = new JdeStrategy(
            populationSize: _populationSize,
            initialMutationForce: initialMutationForce,
            initialCrossoverProbability: initialCrossoverProbability);

        _mutationStrategy = new RandMutationStrategy();
        _controlParameterProvider = jdeStrategy;
        _generationStrategy = jdeStrategy;
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);

        return this;
    }

    /// <summary>
    /// Configures the JADE algorithm (Zhang &amp; Sanderson, 2009): <c>DE/current-to-pbest/1</c>
    /// with an optional external archive and adaptive F/CR. This bundles the mutation,
    /// control-parameter, generation, and selection strategies.
    /// </summary>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size (0 disables the archive).</param>
    /// <param name="adaptationRate">The adaptation rate (c) for the parameter means.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithJade(
        double pBestRate = 0.1,
        double archiveSizeRate = 1.0,
        double adaptationRate = JadeStrategy.DefaultAdaptationRate)
    {
        if (archiveSizeRate < 0.0)
            throw new ArgumentOutOfRangeException(nameof(archiveSizeRate), "Archive size rate must be non-negative.");

        var jadeStrategy = new JadeStrategy(
            populationSize: _populationSize,
            adaptationRate: adaptationRate);

        _mutationStrategy = new CurrentToPBestMutationStrategy(pBestRate);
        _controlParameterProvider = jadeStrategy;
        _generationStrategy = jadeStrategy;
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);
        _archiveCapacity = (int)Math.Round(archiveSizeRate * _populationSize);

        return this;
    }

    /// <summary>
    /// Configures the SHADE algorithm (Tanabe &amp; Fukunaga, 2013): JADE's
    /// <c>DE/current-to-pbest/1</c> with archive, plus success-history based adaptation of
    /// F and CR. This bundles the mutation, control-parameter, generation, and selection strategies.
    /// </summary>
    /// <param name="pBestRate">The upper bound (0, 1] of the per-individual p-best pool fraction
    /// (the SHADE paper uses 0.2). Each trial samples its rate uniformly from <c>[2/N, pBestRate]</c>.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size (0 disables the archive).</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithShade(
        double pBestRate = 0.2,
        double archiveSizeRate = 1.0,
        int memorySize = ShadeStrategy.DefaultMemorySize)
    {
        if (archiveSizeRate < 0.0)
            throw new ArgumentOutOfRangeException(nameof(archiveSizeRate), "Archive size rate must be non-negative.");

        var shadeStrategy = new ShadeStrategy(
            populationSize: _populationSize,
            memorySize: memorySize);

        // SHADE samples p per individual from [2/N, pBestRate]; cap the lower bound at pBestRate
        // so very small populations degenerate to a fixed rate instead of an invalid range.
        var pBestRateMin = Math.Min(2.0 / _populationSize, pBestRate);
        _mutationStrategy = new CurrentToPBestMutationStrategy(pBestRateMin, pBestRate);
        _controlParameterProvider = shadeStrategy;
        _generationStrategy = shadeStrategy;
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);
        _archiveCapacity = (int)Math.Round(archiveSizeRate * _populationSize);

        return this;
    }

    /// <summary>
    /// Configures the L-SHADE algorithm (Tanabe &amp; Fukunaga, 2014): SHADE plus Linear
    /// Population Size Reduction, the CEC-2014 competition winner. The population size given
    /// to <see cref="WithPopulationSize"/> is the initial size and shrinks toward 4 as the
    /// evaluation budget is consumed. Pair with
    /// <see cref="TerminationStrategies.LimitEvaluationNumberTerminationStrategy"/> using the
    /// same budget.
    /// </summary>
    /// <param name="maxEvaluationNumber">The fitness-evaluation budget driving the reduction.</param>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the current population size.</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithLShade(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6)
    {
        if (maxEvaluationNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEvaluationNumber), "Evaluation budget must be greater than 0.");
        if (archiveSizeRate < 0.0)
            throw new ArgumentOutOfRangeException(nameof(archiveSizeRate), "Archive size rate must be non-negative.");

        var lShadeStrategy = new LShadeStrategy(
            initialPopulationSize: _populationSize,
            maxEvaluationNumber: maxEvaluationNumber,
            archiveSizeRate: archiveSizeRate,
            memorySize: memorySize);

        _mutationStrategy = new CurrentToPBestMutationStrategy(pBestRate);
        _controlParameterProvider = lShadeStrategy;
        _generationStrategy = lShadeStrategy;
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);
        _archiveCapacity = (int)Math.Round(archiveSizeRate * _populationSize);
        _lShadeMaxEvaluationNumber = maxEvaluationNumber;

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
            PopulationUpdatedHandler = _populationUpdatedHandler,
            ControlParameterProvider = _controlParameterProvider,
            GenerationStrategy = _generationStrategy,
            BestIndividualIndex = FindBestIndividualIndex(populationFfValues),
            Archive = _archiveCapacity > 0 ? new double[_archiveCapacity * genomeSize] : Memory<double>.Empty,
            ArchiveCapacity = _archiveCapacity,
            EvaluationCount = _populationSize
        };

        PopulationSortHelper.SortIndicesByFitness(
            context.FitnessSortedIndices.Span, populationFfValues, _populationSize, new double[_populationSize]);

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

        if (_populationSize < _mutationStrategy.MinimumPopulationSize)
            throw new InvalidOperationException(
                $"Population size {_populationSize} is too small for the chosen mutation strategy, " +
                $"which needs at least {_mutationStrategy.MinimumPopulationSize} individuals to draw " +
                "the distinct vectors it requires.");

        if (_selectionStrategy == null)
            throw new InvalidOperationException("Selection strategy must be set.");

        if (_terminationStrategy == null)
            throw new InvalidOperationException("Termination strategy must be set.");

        if (_lShadeMaxEvaluationNumber is { } lShadeBudget
            && _terminationStrategy is LimitEvaluationNumberTerminationStrategy evaluationTermination
            && evaluationTermination.MaxEvaluationNumber != lShadeBudget)
            throw new InvalidOperationException(
                $"L-SHADE was configured with an evaluation budget of {lShadeBudget}, but the " +
                $"termination strategy limits evaluations to {evaluationTermination.MaxEvaluationNumber}. " +
                "They must match so the linear population-size reduction reaches its minimum exactly " +
                "as the run terminates.");

        if (_workersCount == 0)
            throw new InvalidOperationException("Workers count must be set.");
    }
    
    /// <summary>
    /// Finds the index of the best (lowest fitness) individual in the initial population.
    /// </summary>
    /// <param name="populationFfValues">The fitness function values of the population.</param>
    /// <returns>The index of the best individual.</returns>
    private static int FindBestIndividualIndex(
        ReadOnlySpan<double> populationFfValues)
    {
        var bestIndividualIndex = 0;
        for (int i = 1; i < populationFfValues.Length; i++)
        {
            if (populationFfValues[i] < populationFfValues[bestIndividualIndex])
                bestIndividualIndex = i;
        }

        return bestIndividualIndex;
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

    /// <summary>Sets the <c>DE/best/1/bin</c> mutation strategy with constant parameters.</summary>
    public ISelectionStrategyRequired WithBestMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>Sets the <c>DE/current-to-best/1/bin</c> mutation strategy with constant parameters.</summary>
    public ISelectionStrategyRequired WithCurrentToBestMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>Sets the <c>DE/rand/2/bin</c> mutation strategy with constant parameters.</summary>
    public ISelectionStrategyRequired WithRandTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>Sets the <c>DE/best/2/bin</c> mutation strategy with constant parameters.</summary>
    public ISelectionStrategyRequired WithBestTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>
    /// Sets the mutation strategy together with the control-parameter provider that supplies
    /// its per-individual F and CR.
    /// </summary>
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy,
        IControlParameterProvider controlParameterProvider);

    /// <summary>
    /// Configures the self-adaptive jDE algorithm (mutation + control parameters + adaptation
    /// + selection in one step).
    /// </summary>
    public ITerminationConditionRequired WithJde(
        double initialMutationForce = JdeStrategy.DefaultInitialMutationForce,
        double initialCrossoverProbability = JdeStrategy.DefaultInitialCrossoverProbability);

    /// <summary>
    /// Configures the JADE algorithm (current-to-pbest/1 with archive and adaptive F/CR).
    /// </summary>
    public ITerminationConditionRequired WithJade(
        double pBestRate = 0.1,
        double archiveSizeRate = 1.0,
        double adaptationRate = JadeStrategy.DefaultAdaptationRate);

    /// <summary>
    /// Configures the SHADE algorithm (current-to-pbest/1 with archive and success-history adaptation).
    /// </summary>
    public ITerminationConditionRequired WithShade(
        double pBestRate = 0.2,
        double archiveSizeRate = 1.0,
        int memorySize = ShadeStrategy.DefaultMemorySize);

    /// <summary>
    /// Configures the L-SHADE algorithm (SHADE plus linear population size reduction).
    /// </summary>
    public ITerminationConditionRequired WithLShade(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6);
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
