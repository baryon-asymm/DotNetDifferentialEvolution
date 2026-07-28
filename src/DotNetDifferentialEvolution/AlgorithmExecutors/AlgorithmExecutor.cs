using DotNetDifferentialEvolution.AlgorithmExecutors.Interfaces;
using DotNetDifferentialEvolution.Helpers;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
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

    private readonly BaseRandomProvider _randomProvider = new RandomProvider();

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

        var population = _context.Population.Span;
        var populationFfValues = _context.PopulationFfValues.Span;
        var nextPopulation = _context.TrialPopulation.Span;
        var nextPopulationFfValues = _context.TrialPopulationFfValues.Span;
        var trialRecords = _context.TrialRecords.Span;

        var fitnessFunctionEvaluator = _context.FitnessFunctionEvaluator;
        var controlParameterProvider = _context.ControlParameterProvider;

        var populationSize = _context.CurrentPopulationSize;
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
                    i, _randomProvider, out mutationForce, out crossoverProbability);
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
                RandomProvider = _randomProvider,
                Archive = archive,
                ArchiveSize = archiveSize,
                FitnessSortedIndices = fitnessSortedIndices
            };

            _mutationStrategy.Mutate(in mutationContext);

            var trialIndividualFfValue = fitnessFunctionEvaluator.Evaluate(
                workerIndex: workerId,
                genes: trialIndividual);

            var parentFfValue = populationFfValues[i];

            _selectionStrategy.Select(
                individualIndex: i,
                trialIndividualFfValue: trialIndividualFfValue,
                trialIndividual: trialIndividual,
                populationFfValues: populationFfValues,
                population: population,
                nextPopulationFfValues: nextPopulationFfValues,
                nextPopulation: nextPopulation);

            trialRecords[i] = new TrialRecord
            {
                Succeeded = trialIndividualFfValue < parentFfValue,
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
