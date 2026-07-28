using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.MutationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;
using DotNetDifferentialEvolution.TerminationStrategies;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Variants;

namespace DotNetDifferentialEvolution.IntegrationTests;

/// <summary>
/// What happened to a trial is the selection strategy's decision, and everything downstream of
/// <see cref="TrialRecord.Outcome"/> — the external archive, jDE's parameter inheritance,
/// JADE/SHADE parameter adaptation, SHADE's improvement weights — depends on being told what
/// actually happened. The executor used to decide for itself with a hardcoded
/// <c>trial &lt; parent</c>, which is only the built-in greedy rule; a selection strategy with any
/// other semantics silently desynchronized the record from the population.
/// </summary>
/// <remarks>
/// Every trial in these runs is an exact copy of its parent, so every trial ties. A tie is the one
/// case where "did it survive?" and "was it an improvement?" disagree, which is exactly why
/// <see cref="SelectionOutcome"/> reports them separately.
/// </remarks>
[Trait("Category", "Integration")]
public class TrialOutcomeReportingTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TheBuiltInStrategyTakesATieWithoutCreditingItAsAnImprovement()
    {
        // The papers' rule, and the reason the two thresholds are separate: SHADE (2013) Eq. (6)
        // and L-SHADE (2014) Algorithm 2 line 12 take the trial on f(u) <= f(x), so the tie
        // survives; line 16 records the success on the strict f(u) < f(x), so it is not a success.
        var recorder = await RunOneGenerationAsync(selectionStrategy: null);

        Assert.NotEmpty(recorder.Outcomes);
        Assert.All(recorder.Outcomes, outcome => Assert.Equal(SelectionOutcome.TrialAccepted, outcome));
    }

    [Fact]
    public async Task ARejectEverythingSelectionStrategyIsReportedAsKeepingTheParent()
    {
        // A strategy stricter than the built-in must not be credited with anything.
        var recorder = await RunOneGenerationAsync(new RejectEverythingSelectionStrategy());

        Assert.NotEmpty(recorder.Outcomes);
        Assert.All(recorder.Outcomes, outcome => Assert.Equal(SelectionOutcome.ParentKept, outcome));
    }

    [Fact]
    public async Task AStrategyThatCallsATieAnImprovementIsReportedAsItClaims()
    {
        // The point of the whole mechanism: the executor relays the strategy's own verdict rather
        // than recomputing one. This strategy is deliberately wrong about its ties — it calls them
        // improvements — and the record must say what it said, because a record that disagreed
        // with the population is the defect this test exists to prevent.
        var recorder = await RunOneGenerationAsync(new OverstatingSelectionStrategy());

        Assert.NotEmpty(recorder.Outcomes);
        Assert.All(recorder.Outcomes, outcome => Assert.Equal(SelectionOutcome.TrialImproved, outcome));
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

    /// <summary>Takes every trial and calls every acceptance an improvement, ties included.</summary>
    private sealed class OverstatingSelectionStrategy : ISelectionStrategy
    {
        public SelectionOutcome Select(
            int individualIndex,
            double trialIndividualFfValue,
            Span<double> trialIndividual,
            Span<double> populationFfValues,
            Span<double> population,
            Span<double> nextPopulationFfValues,
            Span<double> nextPopulation)
        {
            var genomeSize = trialIndividual.Length;
            trialIndividual.CopyTo(nextPopulation.Slice(individualIndex * genomeSize, genomeSize));
            nextPopulationFfValues[individualIndex] = trialIndividualFfValue;

            return SelectionOutcome.TrialImproved;
        }
    }

    /// <summary>Never replaces a parent, whatever the trial scored.</summary>
    private sealed class RejectEverythingSelectionStrategy : ISelectionStrategy
    {
        public SelectionOutcome Select(
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

            return SelectionOutcome.ParentKept;
        }
    }

    private sealed class OutcomeRecorder : IGenerationStrategy
    {
        public List<SelectionOutcome> Outcomes { get; } = [];

        public void AfterGeneration(
            GenerationContext context,
            ReadOnlySpan<TrialRecord> trialRecords)
        {
            ArgumentNullException.ThrowIfNull(context);

            for (int i = 0; i < context.ActivePopulationSize; i++)
                Outcomes.Add(trialRecords[i].Outcome);
        }
    }
}
