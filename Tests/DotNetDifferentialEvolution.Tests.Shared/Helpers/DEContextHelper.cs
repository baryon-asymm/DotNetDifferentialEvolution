using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.Tests.Shared.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.Tests.Shared.Helpers;

public static class DEContextHelper
{
    public static DEContext CreateContext()
    {
        const int populationSize = 300;
        const int boundsSize = 20;
        const double lowerBoundValue = -1.0;
        const double upperBoundValue = 1.0;

        var fFEvaluator = new SimpleSumEvaluator();

        var populationHelper = new PopulationHelper(populationSize, boundsSize);
        
        populationHelper.InitializePopulationWithRandomValues();
        populationHelper.EvaluatePopulationFfValues(fFEvaluator);

        var context = new DEContext
        {
            GenesLowerBound = GenerateBoundsHelper.GenerateBounds(boundsSize, lowerBoundValue),
            GenesUpperBound = GenerateBoundsHelper.GenerateBounds(boundsSize, upperBoundValue),
            FitnessFunctionEvaluator = fFEvaluator,
            GenomeSize = boundsSize,
            PopulationSize = populationSize,
            WorkersCount = 1,
            Population = populationHelper.Population,
            PopulationFfValues = populationHelper.PopulationFfValues,
            TrialPopulation = populationHelper.BufferPopulation,
            TrialPopulationFfValues = populationHelper.BufferPopulationFfValues
        };

        return context;
    }
}
