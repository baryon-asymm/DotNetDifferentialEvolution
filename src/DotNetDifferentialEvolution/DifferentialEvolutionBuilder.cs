using DotNetDifferentialEvolution.AlgorithmExecutors;
using DotNetDifferentialEvolution.Algorithms.Jade;
using DotNetDifferentialEvolution.Algorithms.Jde;
using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.Interfaces;
using DotNetDifferentialEvolution.LocalSearch;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.PopulationSamplingMaker;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies.Interfaces;
using DotNetDifferentialEvolution.Variants;

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

    private IDeVariant? _variant;

    private int _workersCount;

    private IPopulationUpdatedHandler? _populationUpdatedHandler;

    private ILocalSearchRefiner? _localSearchRefiner;
    private int _localSearchInterval = 1;

    private int? _seed;
    
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
    /// Configures a Differential Evolution variant: the mutation operator, where its control
    /// parameters come from, what happens between generations, how trials replace parents and how
    /// large the external archive is, installed as one bundle.
    /// </summary>
    /// <param name="variant">The variant to install.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    /// <remarks>
    /// <see cref="WithJde"/>, <see cref="WithJade"/>, <see cref="WithShade"/> and
    /// <see cref="WithLShade"/> are this method applied to <see cref="JdeVariant"/>,
    /// <see cref="JadeVariant"/>, <see cref="ShadeVariant"/> and <see cref="LShadeVariant"/>, so a
    /// variant written outside this library gets the same treatment as a built-in one: its
    /// mutation strategy's requirements are checked against what it installed, the population size
    /// is checked against the operator's minimum, and its own
    /// <see cref="IDeVariant.Validate"/> runs against the completed configuration.
    /// </remarks>
    public ITerminationConditionRequired WithVariant(
        IDeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);

        var configuration = CreateVariantConfiguration();
        var setup = variant.Configure(in configuration);

        if (setup.MutationStrategy is null)
            throw new InvalidOperationException(
                $"The variant {variant.GetType().Name} produced no mutation strategy.");

        _variant = variant;
        _mutationStrategy = setup.MutationStrategy;
        _controlParameterProvider = setup.ControlParameterProvider;
        _generationStrategy = setup.GenerationStrategy;
        _selectionStrategy = setup.SelectionStrategy ?? new SelectionStrategy(configuration.GenomeSize);
        _archiveCapacity = setup.ArchiveCapacity;

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
        => WithVariant(new JdeVariant(initialMutationForce, initialCrossoverProbability));

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
        => WithVariant(new JadeVariant(pBestRate, archiveSizeRate, adaptationRate));

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
        => WithVariant(new ShadeVariant(pBestRate, archiveSizeRate, memorySize));

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
        => WithVariant(new LShadeVariant(maxEvaluationNumber, pBestRate, archiveSizeRate, memorySize));

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
    /// Registers a local-search refiner that polishes the population in place every
    /// <paramref name="everyNGenerations"/> generations (memetic / hybrid optimization). The
    /// refiner runs single-threaded between generations, after the best individual is identified.
    /// </summary>
    /// <param name="refiner">The local-search refiner.</param>
    /// <param name="everyNGenerations">The generation interval at which the refiner runs (1 = every generation).</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder WithLocalSearch(
        ILocalSearchRefiner refiner,
        int everyNGenerations = 1)
    {
        ArgumentNullException.ThrowIfNull(refiner);

        if (everyNGenerations < 1)
            throw new ArgumentOutOfRangeException(
                nameof(everyNGenerations), "Local-search interval must be at least 1 generation.");

        _localSearchRefiner = refiner;
        _localSearchInterval = everyNGenerations;

        return this;
    }

    /// <summary>
    /// Makes the run reproducible: the same seed, the same configuration and the same number of
    /// workers produce a bit-identical run, initial population included.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    /// <remarks>
    /// <para>
    /// Every worker gets its own generator derived from the seed, which is what makes a parallel
    /// run reproducible at all: the striping is fixed and each individual is built, evaluated and
    /// selected end-to-end by one worker, so no result depends on how the workers interleave.
    /// <strong>The worker count is part of the seed's meaning</strong> — individual <c>i</c> draws
    /// from worker <c>i mod W</c>'s stream, so the same seed at a different
    /// <see cref="IWorkersCountRequired.UseProcessors"/> is a different run. It is reproducible,
    /// not portable across worker counts.
    /// </para>
    /// <para>
    /// <strong>A seed is reproducible only within a minor version.</strong> A change to how the
    /// engine consumes randomness — a different number of draws per trial, say — reshuffles every
    /// seeded run without being a defect in either version.
    /// </para>
    /// <para>
    /// The seed reaches the workers, the population sampler and the generation strategy's own
    /// bookkeeping. A custom <see cref="IPopulationSamplingMaker"/> or
    /// <see cref="IGenerationStrategy"/> receives it through <c>UseRandomProvider</c> and is
    /// reproducible only if it uses what it is given; an <see cref="ILocalSearchRefiner"/> owns
    /// its randomness entirely and must seed itself.
    /// </para>
    /// </remarks>
    public IDifferentialEvolutionBuilder WithSeed(
        int seed)
    {
        _seed = seed;

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

        SeedComponents();

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
            MutationRequirements = _mutationStrategy!.Requirements,
            RandomSeed = _seed,
            LocalSearchRefiner = _localSearchRefiner,
            LocalSearchInterval = _localSearchInterval,
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

        // An unmet ControlParameters requirement is not recoverable at run time: the strategy would
        // read NaN for F and CR, every mutant vector would be NaN, every trial would lose selection,
        // and the run would complete normally having optimized nothing.
        if (_mutationStrategy.Requirements.HasFlag(MutationRequirements.ControlParameters)
            && _controlParameterProvider is null)
            throw new InvalidOperationException(
                $"The mutation strategy {_mutationStrategy.GetType().Name} takes its per-individual " +
                "control parameters (F and CR) from the mutation context, but no control-parameter " +
                "provider was configured. Pass one to WithMutationStrategy(strategy, provider) — " +
                $"{nameof(ConstantControlParameterProvider)} and {nameof(DitheredControlParameterProvider)} " +
                "are built in — or use one of the WithJde/WithJade/WithShade/WithLShade presets. A " +
                "strategy that carries its own F and CR should declare " +
                $"{nameof(MutationRequirements)}.{nameof(MutationRequirements.None)}.");

        if (_selectionStrategy == null)
            throw new InvalidOperationException("Selection strategy must be set.");

        if (_terminationStrategy == null)
            throw new InvalidOperationException("Termination strategy must be set.");

        if (_workersCount == 0)
            throw new InvalidOperationException("Workers count must be set.");

        // Last, so a variant's own cross-checks see a configuration the builder has already
        // agreed is coherent.
        if (_variant is not null)
        {
            var configuration = CreateVariantConfiguration();
            _variant.Validate(in configuration, _terminationStrategy);
        }
    }

    /// <summary>
    /// Hands the seeded random sources to the components that draw outside the workers: the
    /// population sampler, which runs once before the first generation, and the generation
    /// strategy, which runs single-threaded between generations. The workers' own generators are
    /// derived from <see cref="ProblemContext.RandomSeed"/> by the executor.
    /// </summary>
    /// <remarks>
    /// The streams are laid out so none can collide: workers take <c>seed .. seed + W - 1</c>, the
    /// generation strategy takes <c>seed + W</c> and the sampler <c>seed + W + 1</c>. Two
    /// components sharing a stream would still be reproducible, but only by accident — a change
    /// in how one of them draws would silently reshuffle the other.
    /// </remarks>
    private void SeedComponents()
    {
        if (_seed is not { } seed)
            return;

        _generationStrategy?.UseRandomProvider(new SeededRandomProvider(seed + _workersCount));
        _populationSamplingMaker!.UseRandomProvider(new SeededRandomProvider(seed + _workersCount + 1));
    }

    /// <summary>
    /// Captures the problem dimensions a variant is configured and validated against.
    /// </summary>
    private DeVariantConfiguration CreateVariantConfiguration()
        => new(
            PopulationSize: _populationSize,
            GenomeSize: _lowerBound.Length,
            LowerBound: _lowerBound,
            UpperBound: _upperBound);
    
    /// <summary>
    /// Finds the index of the best (lowest fitness) individual in the initial population.
    /// An individual the objective scored NaN never wins; an all-NaN population still yields
    /// a valid index.
    /// </summary>
    /// <param name="populationFfValues">The fitness function values of the population.</param>
    /// <returns>The index of the best individual.</returns>
    private static int FindBestIndividualIndex(
        ReadOnlySpan<double> populationFfValues)
    {
        var bestIndividualIndex = 0;
        for (int i = 1; i < populationFfValues.Length; i++)
        {
            if (FitnessComparisonHelper.IsBetter(populationFfValues[i], populationFfValues[bestIndividualIndex]))
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
    /// Configures a Differential Evolution variant — mutation operator, control parameters,
    /// adaptation, selection and archive — as one bundle. The <c>With…</c> presets below are this
    /// method applied to the built-in variants.
    /// </summary>
    public ITerminationConditionRequired WithVariant(
        IDeVariant variant);

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
    /// Registers a local-search refiner that polishes the population in place every
    /// <paramref name="everyNGenerations"/> generations (memetic / hybrid optimization).
    /// </summary>
    /// <param name="refiner">The local-search refiner.</param>
    /// <param name="everyNGenerations">The generation interval at which the refiner runs (1 = every generation).</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder WithLocalSearch(
        ILocalSearchRefiner refiner,
        int everyNGenerations = 1);

    /// <summary>
    /// Makes the run reproducible: the same seed, the same configuration and the same number of
    /// workers produce a bit-identical run, initial population included.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder WithSeed(
        int seed);

    /// <summary>
    /// Builds the Differential Evolution instance.
    /// </summary>
    /// <returns>An instance of <see cref="DifferentialEvolution"/>.</returns>
    public DifferentialEvolution Build();
}
