using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.Tests.Shared.Helpers;

public static class ProblemContextHelper
{
    public static ProblemContext CreateContext()
    {
        const int populationSize = 300;
        const int boundsSize = 20;
        const double lowerBoundValue = -1.0;
        const double upperBoundValue = 1.0;

        var fFEvaluator = new SimpleSumEvaluator();

        var populationHelper = new PopulationHelper(populationSize, boundsSize);
        
        populationHelper.InitializePopulationWithRandomValues();
        populationHelper.EvaluatePopulationFfValues(fFEvaluator);

        var context = new ProblemContext(
            populationSize: populationSize,
            genomeSize: boundsSize,
            workersCount: 1,
            genesLowerBound: GenerateBoundsHelper.GenerateBounds(boundsSize, lowerBoundValue),
            genesUpperBound: GenerateBoundsHelper.GenerateBounds(boundsSize, upperBoundValue),
            fitnessFunctionEvaluator: fFEvaluator,
            population: populationHelper.Population,
            populationFfValues: populationHelper.PopulationFfValues,
            trialPopulation: populationHelper.TrialPopulation,
            trialPopulationFfValues: populationHelper.TrialPopulationFfValues);

        return context;
    }
}
