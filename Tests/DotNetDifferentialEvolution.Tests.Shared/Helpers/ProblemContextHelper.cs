using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators.Interfaces;

namespace DotNetDifferentialEvolution.Tests.Shared.Helpers;

public static class ProblemContextHelper
{
    public static ProblemContext CreateContext(
        int populationSize,
        ITestFitnessFunctionEvaluator testFitnessFunctionEvaluator)
    {
        var lowerBound = testFitnessFunctionEvaluator.GetLowerBounds();
        var upperBound = testFitnessFunctionEvaluator.GetUpperBounds();
        
        if (lowerBound.Length != upperBound.Length)
            throw new ArgumentException("Lower and upper bounds must have the same size");
        
        var boundsSize = lowerBound.Length;
        var populationHelper = new PopulationHelper(populationSize, boundsSize);
        
        populationHelper.InitializePopulationWithRandomValues(lowerBound.Span, upperBound.Span);
        populationHelper.EvaluatePopulationFfValues(testFitnessFunctionEvaluator);

        var context = new ProblemContext(
            populationSize: populationSize,
            genomeSize: boundsSize,
            workersCount: 1,
            genesLowerBound: lowerBound,
            genesUpperBound: upperBound,
            fitnessFunctionEvaluator: testFitnessFunctionEvaluator,
            population: populationHelper.Population,
            populationFfValues: populationHelper.PopulationFfValues,
            trialPopulation: populationHelper.TrialPopulation,
            trialPopulationFfValues: populationHelper.TrialPopulationFfValues);

        return context;
    }
}
