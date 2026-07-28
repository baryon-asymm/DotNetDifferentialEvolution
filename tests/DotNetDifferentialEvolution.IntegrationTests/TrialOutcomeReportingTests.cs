using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Variants;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// Whether a trial replaced its parent is the selection strategy's decision, and everything
/// downstream of <see cref="TrialRecord.Succeeded"/> — the external archive, JADE/SHADE parameter
/// adaptation, SHADE's improvement weights — depends on being told what actually happened. The
/// executor used to decide for itself with a hardcoded <c>trial &lt; parent</c>, which is only the
/// built-in greedy rule; a selection strategy with any other semantics silently desynchronized
/// the record from the population.
/// </summary>
[Trait("Category", "Integration")]
public class TrialOutcomeReportingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ATieAcceptingSelectionStrategyIsReportedAsSucceeding()
    {
        // Every trial is an exact copy of its parent, so every trial ties. The greedy rule calls
        // that a failure; this strategy calls it a success and takes the trial — and the record
        // has to say what the population now holds.
        var recorder = await RunOneGenerationAsync(new TieAcceptingSelectionStrategy());

        Assert.NotEmpty(recorder.Outcomes);
        Assert.All(recorder.Outcomes, succeeded => Assert.True(succeeded));
    }

    [Fact]
    public async Task ARejectEverythingSelectionStrategyIsReportedAsFailing()
    {
        // The mirror image: the trials are exactly as good as their parents, and a strategy that
        // keeps the parent must not be credited with a success.
        var recorder = await RunOneGenerationAsync(new RejectEverythingSelectionStrategy());

        Assert.NotEmpty(recorder.Outcomes);
        Assert.All(recorder.Outcomes, succeeded => Assert.False(succeeded));
    }

    [Fact]
    public async Task TheBuiltInGreedyStrategyStillReportsTiesAsFailures()
    {
        // The built-in rule is unchanged: a trial has to be strictly better to replace its parent.
        var recorder = await RunOneGenerationAsync(selectionStrategy: null);

        Assert.NotEmpty(recorder.Outcomes);
        Assert.All(recorder.Outcomes, succeeded => Assert.False(succeeded));
    }

    private static async Task<OutcomeRecorder> RunOneGenerationAsync(
        ISelectionStrategy? selectionStrategy)
    {
        var evaluator = new SphereEvaluator(dimension: 3);
        var recorder = new OutcomeRecorder();

        using var de = DifferentialEvolutionBuilder.ForFunction(evaluator)
            .WithBounds(evaluator.GetLowerBounds(), evaluator.GetUpperBounds())
            .WithPopulationSize(8)
            .WithUniformPopulationSampling()
            .WithVariant(new ParentCopyingVariant(selectionStrategy, recorder))
            .WithTerminationCondition(new LimitGenerationNumberTerminationStrategy(1))
            .UseProcessors(1)
            .Build();

        await de.RunAsync().WaitAsync(Timeout);

        return recorder;
    }

    /// <summary>Builds trials that are exact copies of their parents, so every trial ties.</summary>
    private sealed class ParentCopyingVariant : IDeVariant
    {
        private readonly ISelectionStrategy? _selectionStrategy;
        private readonly IGenerationStrategy _generationStrategy;

        public ParentCopyingVariant(
            ISelectionStrategy? selectionStrategy,
            IGenerationStrategy generationStrategy)
        {
            _selectionStrategy = selectionStrategy;
            _generationStrategy = generationStrategy;
        }

        public DeVariantSetup Configure(
            in DeVariantConfiguration configuration)
            => new()
            {
                MutationStrategy = new ParentCopyingMutationStrategy(),
                SelectionStrategy = _selectionStrategy,
                GenerationStrategy = _generationStrategy
            };
    }

    private sealed class ParentCopyingMutationStrategy : IMutationStrategy
    {
        public MutationRequirements Requirements => MutationRequirements.None;

        public void Mutate(
            in MutationContext context)
            => context.Population
                .Slice(context.IndividualIndex * context.GenomeSize, context.GenomeSize)
                .CopyTo(context.TrialIndividual);
    }

    /// <summary>Accepts a trial that merely matches its parent, to keep drifting along plateaus.</summary>
    private sealed class TieAcceptingSelectionStrategy : ISelectionStrategy
    {
        public void Select(
            int individualIndex,
            double trialIndividualFfValue,
            Span<double> trialIndividual,
            Span<double> populationFfValues,
            Span<double> population,
            Span<double> nextPopulationFfValues,
            Span<double> nextPopulation)
            => SelectTrial(
                individualIndex, trialIndividualFfValue, trialIndividual, populationFfValues,
                population, nextPopulationFfValues, nextPopulation);

        public bool SelectTrial(
            int individualIndex,
            double trialIndividualFfValue,
            Span<double> trialIndividual,
            Span<double> populationFfValues,
            Span<double> population,
            Span<double> nextPopulationFfValues,
            Span<double> nextPopulation)
        {
            var genomeSize = trialIndividual.Length;
            var accepted = trialIndividualFfValue <= populationFfValues[individualIndex];

            if (accepted)
                trialIndividual.CopyTo(nextPopulation.Slice(individualIndex * genomeSize, genomeSize));
            else
                population.Slice(individualIndex * genomeSize, genomeSize)
                    .CopyTo(nextPopulation.Slice(individualIndex * genomeSize, genomeSize));

            nextPopulationFfValues[individualIndex] = accepted
                ? trialIndividualFfValue
                : populationFfValues[individualIndex];

            return accepted;
        }
    }

    /// <summary>Never replaces a parent, whatever the trial scored.</summary>
    private sealed class RejectEverythingSelectionStrategy : ISelectionStrategy
    {
        public void Select(
            int individualIndex,
            double trialIndividualFfValue,
            Span<double> trialIndividual,
            Span<double> populationFfValues,
            Span<double> population,
            Span<double> nextPopulationFfValues,
            Span<double> nextPopulation)
            => SelectTrial(
                individualIndex, trialIndividualFfValue, trialIndividual, populationFfValues,
                population, nextPopulationFfValues, nextPopulation);

        public bool SelectTrial(
            int individualIndex,
            double trialIndividualFfValue,
            Span<double> trialIndividual,
            Span<double> populationFfValues,
            Span<double> population,
            Span<double> nextPopulationFfValues,
            Span<double> nextPopulation)
        {
            var genomeSize = trialIndividual.Length;
            population.Slice(individualIndex * genomeSize, genomeSize)
                .CopyTo(nextPopulation.Slice(individualIndex * genomeSize, genomeSize));
            nextPopulationFfValues[individualIndex] = populationFfValues[individualIndex];

            return false;
        }
    }

    private sealed class OutcomeRecorder : IGenerationStrategy
    {
        public List<bool> Outcomes { get; } = [];

        public void AfterGeneration(
            GenerationContext context,
            ReadOnlySpan<TrialRecord> trialRecords)
        {
            ArgumentNullException.ThrowIfNull(context);

            for (int i = 0; i < context.ActivePopulationSize; i++)
                Outcomes.Add(trialRecords[i].Succeeded);
        }
    }
}
