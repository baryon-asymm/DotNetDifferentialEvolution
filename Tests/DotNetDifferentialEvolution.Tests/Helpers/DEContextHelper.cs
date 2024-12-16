using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.Tests.FitnessFunctionEvaluators;

namespace DotNetDifferentialEvolution.Tests.Helpers;

public static class DEContextHelper
{
    public static DEContext CreateContext()
    {
        const int populationSize = 1000;
        const int boundsSize = 100;
        const double lowerBoundValue = -1.0;
        const double upperBoundValue = 1.0;

        var context = new DEContext()
        {
            GenesLowerBound = GenerateBoundsHelper.GenerateBounds(boundsSize, lowerBoundValue),
            GenesUpperBound = GenerateBoundsHelper.GenerateBounds(boundsSize, upperBoundValue),
            FitnessFunctionEvaluator = new SimpleSumEvaluator(),
            GenomeSize = boundsSize,
            PopulationSize = populationSize,
            WorkersCount = 1
        };
        
        return context;
    }
}