using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.UnitTests.SelectionStrategies;

/// <summary>
/// Tests the selection operator of the DE papers: the trial replaces its parent when it is at
/// least as good — or when the parent's fitness is NaN, which counts as worse than every real
/// value — and is reported as an improvement only when it is strictly better. The correct genes
/// and fitness value must land in the next-generation buffers at the right offset.
/// </summary>
[Trait("Category", "Unit")]
public class SelectionStrategyTests
{
    private const int GenomeSize = 2;

    [Fact]
    public void AcceptsTrialWhenStrictlyBetter()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        var strategy = new SelectionStrategy(GenomeSize);

        // Individual 1: parent fitness 9.0, trial fitness 1.0 → accept, and credit the improvement.
        var outcome = strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 1.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(SelectionOutcome.TrialImproved, outcome);
        Assert.Equal(new[] { 70.0, 80.0 }, next[2..4]); // trial genes copied to individual 1's slot
        Assert.Equal(1.0, nextFf[1]);
    }

    [Fact]
    public void KeepsParentWhenTrialIsWorse()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        var strategy = new SelectionStrategy(GenomeSize);

        // Individual 1: parent fitness 9.0, trial fitness 50.0 → reject.
        var outcome = strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 50.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(SelectionOutcome.ParentKept, outcome);
        Assert.Equal(new[] { 30.0, 40.0 }, next[2..4]); // parent genes retained
        Assert.Equal(9.0, nextFf[1]);
    }

    [Fact]
    public void TakesTheTrialWhenFitnessIsEqualButDoesNotCallItAnImprovement()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        var strategy = new SelectionStrategy(GenomeSize);

        // Equal fitness passes the survival test (f(u) <= f(x)), which is what lets a population
        // drift sideways across a plateau instead of freezing on it. It fails the strict test the
        // archive and the parameter adaptation are keyed on, so it is TrialAccepted, not
        // TrialImproved — a zero-gain replacement has taught the search nothing.
        var outcome = strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 9.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(SelectionOutcome.TrialAccepted, outcome);
        Assert.Equal(new[] { 70.0, 80.0 }, next[2..4]); // the trial's genes, not the parent's
        Assert.Equal(9.0, nextFf[1]);
    }

    [Fact]
    public void AcceptsTrialWhenParentFitnessIsNaN()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        populationFf[1] = double.NaN; // the objective returned NaN for individual 1
        var strategy = new SelectionStrategy(GenomeSize);

        // NaN loses every comparison, so an arithmetic rule alone would keep this individual
        // forever. NaN counts as worse than any real value → the real-valued trial wins, and it is
        // a genuine improvement rather than a tie.
        var outcome = strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 50.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(SelectionOutcome.TrialImproved, outcome);
        Assert.Equal(new[] { 70.0, 80.0 }, next[2..4]); // trial genes replaced the NaN individual
        Assert.Equal(50.0, nextFf[1]);
    }

    [Fact]
    public void KeepsParentWhenTrialFitnessIsNaN()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        var strategy = new SelectionStrategy(GenomeSize);

        // A NaN trial is worse than the real-valued parent → reject. It must not slip through the
        // `<=` survival test either: NaN compares false against everything, including itself.
        var outcome = strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: double.NaN,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(SelectionOutcome.ParentKept, outcome);
        Assert.Equal(new[] { 30.0, 40.0 }, next[2..4]);
        Assert.Equal(9.0, nextFf[1]);
    }

    [Fact]
    public void KeepsParentWhenBothFitnessValuesAreNaN()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        populationFf[1] = double.NaN;
        var strategy = new SelectionStrategy(GenomeSize);

        // Two NaNs are not a tie. Nothing is gained by swapping one unusable value for another,
        // and treating it as an acceptance would churn a NaN individual's genes every generation.
        var outcome = strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: double.NaN,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(SelectionOutcome.ParentKept, outcome);
        Assert.Equal(new[] { 30.0, 40.0 }, next[2..4]);
        Assert.True(double.IsNaN(nextFf[1]));
    }

    private static (double[] population, double[] populationFf, double[] trial, double[] next, double[] nextFf) Build()
    {
        double[] population = [10.0, 20.0, 30.0, 40.0]; // individual 0: (10,20), individual 1: (30,40)
        double[] populationFf = [2.0, 9.0];
        double[] trial = [70.0, 80.0];
        var next = new double[4];
        var nextFf = new double[2];

        return (population, populationFf, trial, next, nextFf);
    }
}
