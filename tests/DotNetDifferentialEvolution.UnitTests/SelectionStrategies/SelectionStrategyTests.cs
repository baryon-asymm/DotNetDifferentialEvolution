using DotNetDifferentialEvolution.SelectionStrategies;

namespace DotNetDifferentialEvolution.UnitTests.SelectionStrategies;

/// <summary>
/// Tests greedy selection: the trial replaces its parent only when strictly better, and the
/// correct genes and fitness value land in the next-generation buffers at the right offset.
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

        // Individual 1: parent fitness 9.0, trial fitness 1.0 → accept.
        strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 1.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(new[] { 70.0, 80.0 }, next[2..4]); // trial genes copied to individual 1's slot
        Assert.Equal(1.0, nextFf[1]);
    }

    [Fact]
    public void KeepsParentWhenTrialIsWorse()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        var strategy = new SelectionStrategy(GenomeSize);

        // Individual 1: parent fitness 9.0, trial fitness 50.0 → reject.
        strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 50.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(new[] { 30.0, 40.0 }, next[2..4]); // parent genes retained
        Assert.Equal(9.0, nextFf[1]);
    }

    [Fact]
    public void KeepsParentWhenFitnessIsEqual()
    {
        var (population, populationFf, trial, next, nextFf) = Build();
        var strategy = new SelectionStrategy(GenomeSize);

        // Equal fitness is not strictly better → reject (parent retained).
        strategy.Select(
            individualIndex: 1,
            trialIndividualFfValue: 9.0,
            trialIndividual: trial,
            populationFfValues: populationFf,
            population: population,
            nextPopulationFfValues: nextFf,
            nextPopulation: next);

        Assert.Equal(new[] { 30.0, 40.0 }, next[2..4]);
        Assert.Equal(9.0, nextFf[1]);
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
