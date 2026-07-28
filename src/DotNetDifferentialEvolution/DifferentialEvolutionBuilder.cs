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
    /// Creates a new builder for the specified fitness function evaluator. This is the entry point:
    /// every configuration starts here and the builder is staged, so each call returns only the
    /// steps that are legal next and a run cannot be assembled incompletely or out of order.
    /// </summary>
    /// <param name="fitnessFunctionEvaluator">
    /// The objective, <strong>minimized</strong> — lower is better, and a maximization problem is
    /// posed by returning the negated value. Its worker overload is called concurrently from every
    /// worker thread, so it must either be pure or index per-worker state by the worker index.
    /// </param>
    /// <returns>An instance of <see cref="IBoundsRequired"/> to set the bounds.</returns>
    /// <example>
    /// A complete run, from objective to answer:
    /// <code>
    /// using var de = DifferentialEvolutionBuilder
    ///     .ForFunction(new Sphere())
    ///     .WithBounds(new[] { -5.0, -5.0, -5.0 }, new[] { 5.0, 5.0, 5.0 })
    ///     .WithPopulationSize(50)
    ///     .WithUniformPopulationSampling()
    ///     .WithDefaultMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)
    ///     .WithDefaultSelectionStrategy()
    ///     .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(200))
    ///     .UseAllProcessors()
    ///     .Build();
    ///
    /// var population = await de.RunAsync();
    ///
    /// population.MoveCursorToBestIndividual();
    /// var best = population.IndividualCursor.GetSnapshot(deepCopy: true);
    /// </code>
    /// Substituting one of <see cref="IMutationStrategyRequired.WithJde"/>,
    /// <see cref="IMutationStrategyRequired.WithJade"/>, <see cref="IMutationStrategyRequired.WithShade"/>
    /// or <see cref="IMutationStrategyRequired.WithLShade"/> for the mutation and selection calls
    /// gives a self-adaptive algorithm instead; those presets install their own selection rule, so
    /// the chain goes straight on to the termination condition.
    /// <para>
    /// <see cref="DifferentialEvolution"/> owns worker threads and is single-use: dispose it, and
    /// build a new one to search again.
    /// </para>
    /// </example>
    public static IBoundsRequired ForFunction(
        IFitnessFunctionEvaluator fitnessFunctionEvaluator)
    {
        return new DifferentialEvolutionBuilder(fitnessFunctionEvaluator);
    }
    
    /// <inheritdoc />
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

    /// <inheritdoc />
    public IPopulationSamplingRequired WithPopulationSize(
        int populationSize)
    {
        if (populationSize <= 0)
            throw new ArgumentException("Population size must be greater than 0.");
        
        _populationSize = populationSize;
        
        return this;
    }

    /// <inheritdoc />
    public IMutationStrategyRequired WithPopulationSampling(
        IPopulationSamplingMaker populationSamplingMaker)
    {
        ArgumentNullException.ThrowIfNull(populationSamplingMaker);
        
        _populationSamplingMaker = populationSamplingMaker;
        
        return this;
    }

    /// <inheritdoc />
    public IMutationStrategyRequired WithUniformPopulationSampling()
    {
        _populationSamplingMaker = new UniformRandomSamplingMaker(_lowerBound, _upperBound);
        
        return this;
    }

    /// <inheritdoc />
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy)
    {
        ArgumentNullException.ThrowIfNull(mutationStrategy);
        
        _mutationStrategy = mutationStrategy;
        
        return this;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ISelectionStrategyRequired WithBestMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new BestMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <inheritdoc />
    public ISelectionStrategyRequired WithCurrentToBestMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new CurrentToBestMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <inheritdoc />
    public ISelectionStrategyRequired WithRandTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new RandTwoMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <inheritdoc />
    public ISelectionStrategyRequired WithBestTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability)
        => WithMutationStrategy(
            new BestTwoMutationStrategy(),
            new ConstantControlParameterProvider(mutationForce, crossoverProbability));

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ITerminationConditionRequired WithJde(
        double initialMutationForce = JdeStrategy.DefaultInitialMutationForce,
        double initialCrossoverProbability = JdeStrategy.DefaultInitialCrossoverProbability)
        => WithVariant(new JdeVariant(initialMutationForce, initialCrossoverProbability));

    /// <inheritdoc />
    public ITerminationConditionRequired WithJade(
        double pBestRate = 0.1,
        double archiveSizeRate = 1.0,
        double adaptationRate = JadeStrategy.DefaultAdaptationRate)
        => WithVariant(new JadeVariant(pBestRate, archiveSizeRate, adaptationRate));

    /// <inheritdoc />
    public ITerminationConditionRequired WithShade(
        double pBestRate = 0.2,
        double archiveSizeRate = 1.0,
        int memorySize = ShadeStrategy.DefaultMemorySize)
        => WithVariant(new ShadeVariant(pBestRate, archiveSizeRate, memorySize));

    /// <inheritdoc />
    public ITerminationConditionRequired WithLShade(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6)
        => WithVariant(new LShadeVariant(maxEvaluationNumber, pBestRate, archiveSizeRate, memorySize));

    /// <inheritdoc />
    public ITerminationConditionRequired WithSelectionStrategy(
        ISelectionStrategy selectionStrategy)
    {
        ArgumentNullException.ThrowIfNull(selectionStrategy);
        
        _selectionStrategy = selectionStrategy;
        
        return this;
    }

    /// <inheritdoc />
    public ITerminationConditionRequired WithDefaultSelectionStrategy()
    {
        _selectionStrategy = new SelectionStrategy(_lowerBound.Length);
        
        return this;
    }

    /// <inheritdoc />
    public IWorkersCountRequired WithTerminationCondition(
        ITerminationStrategy terminationStrategy)
    {
        ArgumentNullException.ThrowIfNull(terminationStrategy);
        
        _terminationStrategy = terminationStrategy;
        
        return this;
    }

    /// <inheritdoc />
    public IDifferentialEvolutionBuilder UseProcessors(
        int processorsCount)
    {
        if (processorsCount <= 0)
            throw new ArgumentException("Processors count must be greater than 0.");
        
        _workersCount = processorsCount;
        
        return this;
    }

    /// <inheritdoc />
    public IDifferentialEvolutionBuilder UseAllProcessors()
    {
        _workersCount = Environment.ProcessorCount;
        
        return this;
    }

    /// <inheritdoc />
    public IDifferentialEvolutionBuilder WithPopulationUpdateHandler(
        IPopulationUpdatedHandler populationUpdatedHandler)
    {
        _populationUpdatedHandler = populationUpdatedHandler;

        return this;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public IDifferentialEvolutionBuilder WithSeed(
        int seed)
    {
        _seed = seed;

        return this;
    }

    /// <inheritdoc />
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

/*
   The staged-builder interfaces below are the surface a consumer actually sees. Because every
   builder call returns one of these rather than the concrete class, an IDE tooltip — and anything
   else reading the shipped XML documentation — resolves to the declaration here, never to
   DifferentialEvolutionBuilder's own members. These declarations are therefore the authoritative
   documentation for the whole fluent API, and the implementing members carry <inheritdoc />.
*/

/// <summary>
/// The first stage of the builder: the search space. Reached from
/// <see cref="DifferentialEvolutionBuilder.ForFunction"/>.
/// </summary>
public interface IBoundsRequired
{
    /// <summary>
    /// Sets the per-gene box the search is confined to, and with it the genome size — the length of
    /// these two arrays is the number of variables the objective will receive.
    /// </summary>
    /// <param name="lowerBound">The inclusive lower bound of each gene.</param>
    /// <param name="upperBound">The inclusive upper bound of each gene.</param>
    /// <returns>An instance of <see cref="IPopulationSizeRequired"/> to set the population size.</returns>
    /// <exception cref="ArgumentException">
    /// The two lengths differ, or some gene has a lower bound above its upper bound.
    /// </exception>
    /// <remarks>
    /// The box is the only constraint the library understands. A gene driven outside it by mutation
    /// is repaired to the midpoint between the violated bound and the parent's value before the
    /// objective ever sees it, so the objective is never called with an out-of-box vector. Any other
    /// kind of constraint has to be encoded in the objective itself.
    /// </remarks>
    public IPopulationSizeRequired WithBounds(
        ReadOnlyMemory<double> lowerBound,
        ReadOnlyMemory<double> upperBound);
}

/// <summary>
/// The second stage of the builder: how many individuals evolve.
/// </summary>
public interface IPopulationSizeRequired
{
    /// <summary>
    /// Sets the number of individuals in the population.
    /// </summary>
    /// <param name="populationSize">
    /// The size of the population, which must be positive and large enough for the mutation operator
    /// chosen later to draw the distinct individuals it needs — 4 for <c>rand/1</c> and for the
    /// <c>current-to-pbest/1</c> used by JADE, SHADE and L-SHADE, 3 for <c>best/1</c> and
    /// <c>current-to-best/1</c>, 5 for <c>best/2</c>, 6 for <c>rand/2</c>. The mismatch is caught by
    /// <see cref="IDifferentialEvolutionBuilder.Build"/>, which names both numbers.
    /// </param>
    /// <returns>An instance of <see cref="IPopulationSamplingRequired"/> to set the population sampling method.</returns>
    /// <exception cref="ArgumentException">The population size is not positive.</exception>
    /// <remarks>
    /// Under <see cref="IMutationStrategyRequired.WithLShade"/> this is the <em>initial</em> size:
    /// linear population size reduction shrinks it toward 4 as the evaluation budget is consumed.
    /// </remarks>
    public IPopulationSamplingRequired WithPopulationSize(
        int populationSize);
}

/// <summary>
/// The third stage of the builder: where the first generation comes from.
/// </summary>
public interface IPopulationSamplingRequired
{
    /// <summary>
    /// Seeds the first generation from a custom sampler — a warm start from known-good solutions,
    /// a low-discrepancy sequence, or any other scheme.
    /// </summary>
    /// <param name="populationSamplingMaker">The population sampling maker.</param>
    /// <returns>An instance of <see cref="IMutationStrategyRequired"/> to set the mutation strategy.</returns>
    /// <remarks>
    /// A custom sampler is offered the seeded random source and is reproducible only if it draws
    /// from what it is given. It is responsible for staying inside the bounds.
    /// </remarks>
    public IMutationStrategyRequired WithPopulationSampling(
        IPopulationSamplingMaker populationSamplingMaker);

    /// <summary>
    /// Draws the first generation uniformly at random from the box — the standard choice, and the
    /// one every paper in this library assumes.
    /// </summary>
    /// <returns>An instance of <see cref="IMutationStrategyRequired"/> to set the mutation strategy.</returns>
    public IMutationStrategyRequired WithUniformPopulationSampling();
}

/// <summary>
/// The fourth stage of the builder, and the one that decides which algorithm this is. Either pick a
/// mutation operator with constant control parameters and go on to choose a selection rule, or take
/// one of the self-adaptive presets, which install the operator, the control parameters, the
/// adaptation and the selection rule together and skip the selection stage entirely.
/// </summary>
/// <remarks>
/// Unsure which to take? <see cref="WithLShade"/> when the run is budgeted in evaluations — it won
/// the CEC-2014 competition — and <see cref="WithShade"/> when it is not. The constant-parameter
/// operators are the right choice when F and CR are being tuned deliberately, or as a baseline to
/// measure an adaptive variant against.
/// </remarks>
public interface IMutationStrategyRequired
{
    /// <summary>
    /// Sets a custom mutation operator whose F and CR it supplies itself, which it declares by
    /// returning <see cref="MutationRequirements.None"/> from its requirements.
    /// </summary>
    /// <param name="mutationStrategy">The mutation strategy.</param>
    /// <returns>An instance of <see cref="ISelectionStrategyRequired"/> to set the selection strategy.</returns>
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy);

    /// <summary>
    /// Sets <c>DE/rand/1/bin</c>, the classic scheme of Storn and Price, with F and CR held
    /// constant for the whole run. Needs a population of at least 4.
    /// </summary>
    /// <param name="mutationForce">
    /// F, the scale applied to the difference of two randomly chosen individuals.
    /// </param>
    /// <param name="crossoverProbability">
    /// CR, the per-gene probability of taking the mutant's gene rather than the parent's. One gene
    /// chosen at random is always taken, so a trial is never an exact copy of its parent.
    /// </param>
    /// <returns>An instance of <see cref="ISelectionStrategyRequired"/> to set the selection strategy.</returns>
    public ISelectionStrategyRequired WithDefaultMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>
    /// Sets <c>DE/best/1/bin</c> with constant parameters: the base vector is the current best, so
    /// the search converges faster and is likelier to be trapped. Needs a population of at least 3.
    /// </summary>
    public ISelectionStrategyRequired WithBestMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>
    /// Sets <c>DE/current-to-best/1/bin</c> with constant parameters: each individual moves partway
    /// toward the current best. Needs a population of at least 3.
    /// </summary>
    public ISelectionStrategyRequired WithCurrentToBestMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>
    /// Sets <c>DE/rand/2/bin</c> with constant parameters: two difference vectors instead of one,
    /// which explores more and converges more slowly. Needs a population of at least 6.
    /// </summary>
    public ISelectionStrategyRequired WithRandTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>
    /// Sets <c>DE/best/2/bin</c> with constant parameters. Needs a population of at least 5.
    /// </summary>
    public ISelectionStrategyRequired WithBestTwoMutationStrategy(
        double mutationForce,
        double crossoverProbability);

    /// <summary>
    /// Sets the mutation strategy together with the control-parameter provider that supplies
    /// its per-individual F and CR.
    /// </summary>
    /// <remarks>
    /// This is the pairing an operator declaring <see cref="MutationRequirements.ControlParameters"/>
    /// requires; supplying such an operator through the single-argument overload is rejected by
    /// <see cref="IDifferentialEvolutionBuilder.Build"/> rather than silently producing NaN trials.
    /// <see cref="ConstantControlParameterProvider"/> and <see cref="DitheredControlParameterProvider"/>
    /// are built in.
    /// </remarks>
    public ISelectionStrategyRequired WithMutationStrategy(
        IMutationStrategy mutationStrategy,
        IControlParameterProvider controlParameterProvider);

    /// <summary>
    /// Configures a Differential Evolution variant: the mutation operator, where its control
    /// parameters come from, what happens between generations, how trials replace parents and how
    /// large the external archive is, installed as one bundle.
    /// </summary>
    /// <param name="variant">The variant to install.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    /// <remarks>
    /// <para>
    /// Prefer this over assembling the pieces by hand whenever they depend on one another: an
    /// adaptive scheme is meaningless without the operator that reads the parameters it adapts, and
    /// a variant is validated as a unit.
    /// </para>
    /// <para>
    /// <see cref="WithJde"/>, <see cref="WithJade"/>, <see cref="WithShade"/> and
    /// <see cref="WithLShade"/> are this method applied to <see cref="JdeVariant"/>,
    /// <see cref="JadeVariant"/>, <see cref="ShadeVariant"/> and <see cref="LShadeVariant"/>, so a
    /// variant written outside this library gets the same treatment as a built-in one: its
    /// mutation strategy's requirements are checked against what it installed, the population size
    /// is checked against the operator's minimum, and its own
    /// <see cref="IDeVariant.Validate"/> runs against the completed configuration.
    /// </para>
    /// </remarks>
    public ITerminationConditionRequired WithVariant(
        IDeVariant variant);

    /// <summary>
    /// Configures jDE (Brest et al., 2006): <c>DE/rand/1/bin</c> in which every individual carries
    /// its own F and CR, re-randomized with a small probability each generation and inherited
    /// whenever the trial survives. Self-adapting and budget-agnostic; needs a population of at
    /// least 4.
    /// </summary>
    /// <param name="initialMutationForce">The initial mutation factor for every individual.</param>
    /// <param name="initialCrossoverProbability">The initial crossover probability for every individual.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithJde(
        double initialMutationForce = JdeStrategy.DefaultInitialMutationForce,
        double initialCrossoverProbability = JdeStrategy.DefaultInitialCrossoverProbability);

    /// <summary>
    /// Configures JADE (Zhang &amp; Sanderson, 2009): <c>DE/current-to-pbest/1</c> with an optional
    /// archive of displaced parents, and F and CR drawn each generation from distributions whose
    /// means follow the parameters that recently succeeded. Needs a population of at least 4.
    /// </summary>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size (0 disables the archive).</param>
    /// <param name="adaptationRate">The adaptation rate (c) for the parameter means.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    /// <remarks>
    /// Alone among the variants here, JADE keeps the parent when a trial merely <em>ties</em> it —
    /// its Table I says so, and its unweighted parameter means are why it can afford to. Do not
    /// read the difference from SHADE and L-SHADE as an inconsistency.
    /// </remarks>
    public ITerminationConditionRequired WithJade(
        double pBestRate = 0.1,
        double archiveSizeRate = 1.0,
        double adaptationRate = JadeStrategy.DefaultAdaptationRate);

    /// <summary>
    /// Configures SHADE (Tanabe &amp; Fukunaga, 2013): JADE's operator and archive, with the single
    /// adaptive mean replaced by a memory of H recent successful settings, and a p-best fraction
    /// drawn per individual. A stronger default than JADE and equally budget-agnostic; needs a
    /// population of at least 4.
    /// </summary>
    /// <param name="pBestRate">The upper bound (0, 1] of the per-individual p-best pool fraction
    /// (the SHADE paper uses 0.2). Each trial samples its rate uniformly from <c>[2/N, pBestRate]</c>.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the population size (0 disables the archive).</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    /// <remarks>
    /// This is <strong>SHADE 1.0</strong>, the CEC-2013 paper. The code the authors distribute under
    /// the name SHADE is version 1.1, which changed the memory update; that later revision is what
    /// <see cref="WithLShade"/> is built on, as the L-SHADE paper specifies. Differences against the
    /// distributed sources are deliberate and are listed in the package's <c>docs/ALGORITHMS.md</c>.
    /// </remarks>
    public ITerminationConditionRequired WithShade(
        double pBestRate = 0.2,
        double archiveSizeRate = 1.0,
        int memorySize = ShadeStrategy.DefaultMemorySize);

    /// <summary>
    /// Configures L-SHADE (Tanabe &amp; Fukunaga, 2014): SHADE 1.1 plus linear population size
    /// reduction, and the CEC-2014 competition winner. The size given to
    /// <see cref="IPopulationSizeRequired.WithPopulationSize"/> is the initial one and shrinks
    /// toward 4 as the evaluation budget is spent, so late generations are a small, intensifying
    /// population.
    /// </summary>
    /// <param name="maxEvaluationNumber">
    /// The evaluation budget the reduction schedule is planned against. It must equal the budget of
    /// the <see cref="TerminationStrategies.LimitEvaluationNumberTerminationStrategy"/> that stops
    /// the run.
    /// </param>
    /// <param name="pBestRate">The fraction (0, 1] of top individuals forming the p-best pool.</param>
    /// <param name="archiveSizeRate">The archive capacity as a multiple of the current population size.</param>
    /// <param name="memorySize">The size of the success-history memory (H).</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown from <see cref="IDifferentialEvolutionBuilder.Build"/> when this budget and the
    /// termination strategy's budget differ. Both numbers are named in the message.
    /// </exception>
    /// <example>
    /// Pass one constant to both, so the schedule reaches its minimum exactly as the run ends:
    /// <code>
    /// const long Budget = 300_000;
    /// const int Dimensions = 30;
    ///
    /// using var de = DifferentialEvolutionBuilder
    ///     .ForFunction(objective)
    ///     .WithBounds(lowerBound, upperBound)
    ///     .WithPopulationSize(18 * Dimensions)   // r_N^init = 18 from the paper's Table II
    ///     .WithUniformPopulationSampling()
    ///     .WithLShade(maxEvaluationNumber: Budget)
    ///     .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(Budget))
    ///     .UseAllProcessors()
    ///     .Build();
    /// </code>
    /// Pairing L-SHADE with a generation or stagnation limit instead is <em>not</em> rejected and
    /// cannot be — the check has no second budget to compare against. The run then either ends with
    /// the population still far above its floor, or spends its tail collapsed at 4 individuals.
    /// </example>
    public ITerminationConditionRequired WithLShade(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6);
}

/// <summary>
/// The fifth stage of the builder: which of the parent and its trial survives. Only reached when
/// the mutation operator was chosen directly — the self-adaptive presets install their own rule and
/// skip this stage.
/// </summary>
public interface ISelectionStrategyRequired
{
    /// <summary>
    /// Sets a custom survival rule.
    /// </summary>
    /// <param name="selectionStrategy">The selection strategy.</param>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithSelectionStrategy(
        ISelectionStrategy selectionStrategy);

    /// <summary>
    /// Sets the greedy rule of the DE papers: the trial replaces its parent when it is at least as
    /// good, so the population can drift across a plateau instead of freezing on it, and a run can
    /// never get worse.
    /// </summary>
    /// <returns>An instance of <see cref="ITerminationConditionRequired"/> to set the termination condition.</returns>
    public ITerminationConditionRequired WithDefaultSelectionStrategy();
}

/// <summary>
/// The sixth stage of the builder: when to stop.
/// </summary>
public interface ITerminationConditionRequired
{
    /// <summary>
    /// Sets the stopping condition, checked once per generation at the barrier. The built-in
    /// choices are <see cref="TerminationStrategies.LimitGenerationNumberTerminationStrategy"/>,
    /// <see cref="TerminationStrategies.LimitEvaluationNumberTerminationStrategy"/> — the one
    /// L-SHADE requires — and <see cref="TerminationStrategies.StagnationStreakTerminationStrategy"/>.
    /// </summary>
    /// <param name="terminationStrategy">The termination strategy.</param>
    /// <returns>An instance of <see cref="IWorkersCountRequired"/> to set the number of workers.</returns>
    public IWorkersCountRequired WithTerminationCondition(
        ITerminationStrategy terminationStrategy);
}

/// <summary>
/// The seventh stage of the builder: how much of the machine to use.
/// </summary>
public interface IWorkersCountRequired
{
    /// <summary>
    /// Sets the number of worker threads. Each owns a stripe of the population and its own random
    /// stream, so this number is part of the meaning of a seed.
    /// </summary>
    /// <param name="processorsCount">The number of processors, which must be positive.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    /// <exception cref="ArgumentException">The processor count is not positive.</exception>
    /// <remarks>
    /// Parallelism pays for itself when the objective is expensive; for a very cheap objective the
    /// per-generation barrier can cost more than it saves, and a single worker may be faster.
    /// </remarks>
    public IDifferentialEvolutionBuilder UseProcessors(
        int processorsCount);

    /// <summary>
    /// Uses every processor the host reports. Convenient, but it makes the worker count — and so
    /// any seeded run — depend on the machine; see <see cref="IDifferentialEvolutionBuilder.WithSeed"/>.
    /// </summary>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    public IDifferentialEvolutionBuilder UseAllProcessors();
}

/// <summary>
/// The final stage of the builder: everything still to configure is optional, and
/// <see cref="Build"/> is available.
/// </summary>
public interface IDifferentialEvolutionBuilder
{
    /// <summary>
    /// Observes the population at the end of every generation — progress reporting, logging, early
    /// bookkeeping.
    /// </summary>
    /// <param name="populationUpdatedHandler">The population update handler.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    /// <remarks>
    /// The handler runs at the generation barrier, on the thread that reached it, while every worker
    /// waits. Keep it cheap: whatever it spends is spent once per generation by the whole run. The
    /// <see cref="Population"/> it receives is a live cursor-based view, so anything that must
    /// outlive the call has to be copied out of it.
    /// </remarks>
    public IDifferentialEvolutionBuilder WithPopulationUpdateHandler(
        IPopulationUpdatedHandler populationUpdatedHandler);

    /// <summary>
    /// Registers a local-search refiner that polishes the population in place every
    /// <paramref name="everyNGenerations"/> generations (memetic / hybrid optimization). It runs
    /// single-threaded between generations, after the best individual is identified, with read and
    /// write access to the population; its own evaluations count toward the budget.
    /// </summary>
    /// <param name="refiner">The local-search refiner.</param>
    /// <param name="everyNGenerations">The generation interval at which the refiner runs (1 = every generation).</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The interval is less than 1.</exception>
    /// <remarks>
    /// This is the seam a local optimizer such as Nelder–Mead plugs into. A refiner owns its own
    /// randomness and must seed itself to stay reproducible.
    /// </remarks>
    public IDifferentialEvolutionBuilder WithLocalSearch(
        ILocalSearchRefiner refiner,
        int everyNGenerations = 1);

    /// <summary>
    /// Makes the run reproducible: the same seed, the same configuration and the same number of
    /// workers produce a bit-identical run, initial population included, however the workers'
    /// threads happen to interleave.
    /// </summary>
    /// <param name="seed">The seed.</param>
    /// <returns>An instance of <see cref="IDifferentialEvolutionBuilder"/> to build the Differential Evolution instance.</returns>
    /// <remarks>
    /// <para>
    /// <strong>The worker count is part of the seed's meaning.</strong> Individual <c>i</c> draws
    /// from worker <c>i mod W</c>'s stream, so the same seed at a different
    /// <see cref="IWorkersCountRequired.UseProcessors"/> is a different run — reproducible, but not
    /// portable across worker counts. A seeded run that must reproduce elsewhere should pin
    /// <see cref="IWorkersCountRequired.UseProcessors"/> rather than take
    /// <see cref="IWorkersCountRequired.UseAllProcessors"/>, which resolves to whatever the host has.
    /// </para>
    /// <para>
    /// <strong>A seed is reproducible only within a minor version.</strong> A change to how the
    /// engine consumes randomness — a different number of draws per trial, say — reshuffles every
    /// seeded run without being a defect in either version.
    /// </para>
    /// <para>
    /// The seed covers the initial population, mutation and crossover, control-parameter sampling
    /// and archive eviction. A custom <see cref="IPopulationSamplingMaker"/> or
    /// <see cref="IGenerationStrategy"/> receives it and is reproducible only if it draws from what
    /// it is given; an <see cref="ILocalSearchRefiner"/> owns its randomness entirely.
    /// </para>
    /// </remarks>
    public IDifferentialEvolutionBuilder WithSeed(
        int seed);

    /// <summary>
    /// Validates the whole configuration and builds the runnable instance.
    /// </summary>
    /// <returns>An instance of <see cref="DifferentialEvolution"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// The configuration is internally inconsistent in a way no type could express: the population
    /// is too small for the chosen operator, an operator that reads per-individual F and CR was
    /// given no control-parameter provider, or L-SHADE's budget does not match the termination
    /// strategy's. Every message names what to change.
    /// </exception>
    /// <remarks>
    /// The result owns worker threads: dispose it. It is also single-use — a second
    /// <see cref="DifferentialEvolution.RunAsync()"/> returns the already-completed task rather than
    /// starting a new search, so searching again means building again.
    /// </remarks>
    public DifferentialEvolution Build();
}
