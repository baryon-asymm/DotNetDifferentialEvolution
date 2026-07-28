using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.RandomProviders;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.AlgorithmExecutors;

/// <summary>
/// Class for executing the differential evolution algorithm.
/// </summary>
public class AlgorithmExecutor : IAlgorithmExecutor
{
    private readonly int _individualHandlerStepSize;

    private readonly IMutationStrategy _mutationStrategy;
    private readonly ISelectionStrategy _selectionStrategy;

    private readonly ProblemContext _context;

    /// <summary>
    /// One random provider per worker, indexed by worker id. Each worker owns its own stream, so
    /// no draw is contended and — when the run is seeded — no draw depends on how the workers
    /// interleave. The striping is fixed (worker <c>k</c> handles <c>{k, k+W, …}</c>) and each
    /// individual is built, evaluated and selected end-to-end by one worker, so a seeded run is
    /// bit-reproducible <em>for a given worker count</em>.
    /// </summary>
    /// <remarks>
    /// Reproducibility does not survive a change of worker count, and cannot: individual <c>i</c>
    /// draws from worker <c>i mod W</c>'s stream, so changing <c>W</c> changes which numbers each
    /// individual sees. That is the price of per-worker streams, and per-worker streams are what
    /// make a parallel run reproducible at all.
    /// </remarks>
    private readonly SeededRandomProvider[] _randomProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmExecutor"/> class.
    /// </summary>
    /// <param name="mutationStrategy">The mutation strategy to be used.</param>
    /// <param name="selectionStrategy">The selection strategy to be used.</param>
    /// <param name="context">The problem context containing population and other parameters.</param>
    public AlgorithmExecutor(
        IMutationStrategy mutationStrategy,
        ISelectionStrategy selectionStrategy,
        ProblemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutationStrategy);

        // The builder rejects this pairing before it gets here; a hand-assembled ProblemContext
        // does not go through the builder, and the failure it produces — NaN control parameters,
        // NaN trial vectors, every trial rejected, a run that reports the initial sample as its
        // optimum — is silent enough to be worth a second guard.
        if (mutationStrategy.Requirements.HasFlag(MutationRequirements.ControlParameters)
            && context.ControlParameterProvider is null)
            throw new InvalidOperationException(
                $"The mutation strategy {mutationStrategy.GetType().Name} requires per-individual " +
                "control parameters, but the problem context has no control-parameter provider.");

        _individualHandlerStepSize = context.WorkersCount;

        _mutationStrategy = mutationStrategy;
        _selectionStrategy = selectionStrategy;

        _context = context;

        // An unseeded run still gets a seed — drawn once here — rather than sharing
        // Random.Shared. The point is per-worker state: Random.Shared is thread-safe, but every
        // call walks a thread-static indirection, and the workers would be drawing from one
        // generator whose interleaving nothing controls.
        var rootSeed = context.RandomSeed ?? Random.Shared.Next();

        _randomProviders = new SeededRandomProvider[context.WorkersCount];
        for (int workerId = 0; workerId < _randomProviders.Length; workerId++)
            _randomProviders[workerId] = new SeededRandomProvider(rootSeed + workerId);
    }

    /// <summary>
    /// Executes the algorithm.
    /// </summary>
    /// <param name="workerId">The index of the worker executing the algorithm.</param>
    /// <param name="bestHandledIndividualIndex">The index of the best handled individual.</param>
    public void Execute(
        int workerId,
        out int bestHandledIndividualIndex)
    {
        var genomeSize = _context.GenomeSize;
        Span<double> trialIndividual = stackalloc double[genomeSize];

        var randomProvider = _randomProviders[workerId];

        var currentPopulation = _context.CurrentPopulation;
        var nextGeneration = _context.TrialPopulation;

        var population = currentPopulation.Genes.Span;
        var populationFfValues = currentPopulation.FfValues.Span;
        var nextPopulation = nextGeneration.Genes.Span;
        var nextPopulationFfValues = nextGeneration.FfValues.Span;
        var trialRecords = _context.TrialRecords.Span;

        var fitnessFunctionEvaluator = _context.FitnessFunctionEvaluator;
        var controlParameterProvider = _context.ControlParameterProvider;

        var populationSize = currentPopulation.Count;
        var bestIndividualIndex = _context.BestIndividualIndex;
        var lowerBound = _context.GenesLowerBound.Span;
        var upperBound = _context.GenesUpperBound.Span;

        var archive = _context.Archive.Span;
        var archiveSize = _context.ArchiveSize;
        var fitnessSortedIndices = _context.FitnessSortedIndices.Span.Slice(0, populationSize);

        bestHandledIndividualIndex = workerId < populationSize ? workerId : 0;
        for (var i = workerId; i < populationSize; i += _individualHandlerStepSize)
        {
            double mutationForce, crossoverProbability;
            if (controlParameterProvider is null)
            {
                mutationForce = double.NaN;
                crossoverProbability = double.NaN;
            }
            else
            {
                controlParameterProvider.GetControlParameters(
                    i, randomProvider, out mutationForce, out crossoverProbability);
            }

            var mutationContext = new MutationContext
            {
                IndividualIndex = i,
                BestIndividualIndex = bestIndividualIndex,
                PopulationSize = populationSize,
                GenomeSize = genomeSize,
                MutationForce = mutationForce,
                CrossoverProbability = crossoverProbability,
                Population = population,
                PopulationFfValues = populationFfValues,
                TrialIndividual = trialIndividual,
                LowerBound = lowerBound,
                UpperBound = upperBound,
                RandomProvider = randomProvider,
                WorkerRandomProvider = randomProvider,
                Archive = archive,
                ArchiveSize = archiveSize,
                FitnessSortedIndices = fitnessSortedIndices
            };

            _mutationStrategy.Mutate(in mutationContext);

            var trialIndividualFfValue = fitnessFunctionEvaluator.Evaluate(
                workerIndex: workerId,
                genes: trialIndividual);

            var parentFfValue = populationFfValues[i];

            // The selection strategy reports its own decision: what happened to the trial is its
            // rule to apply, and the archive and parameter adaptation downstream need the outcome
            // that actually happened, not the greedy rule assumed here.
            var outcome = _selectionStrategy.Select(
                individualIndex: i,
                trialIndividualFfValue: trialIndividualFfValue,
                trialIndividual: trialIndividual,
                populationFfValues: populationFfValues,
                population: population,
                nextPopulationFfValues: nextPopulationFfValues,
                nextPopulation: nextPopulation);

            trialRecords[i] = new TrialRecord
            {
                Outcome = outcome,
                UsedF = mutationForce,
                UsedCr = crossoverProbability,
                ParentFfValue = parentFfValue,
                TrialFfValue = trialIndividualFfValue
            };

            if (FitnessComparisonHelper.IsBetter(
                    nextPopulationFfValues[i], nextPopulationFfValues[bestHandledIndividualIndex]))
                bestHandledIndividualIndex = i;
        }
    }
}
