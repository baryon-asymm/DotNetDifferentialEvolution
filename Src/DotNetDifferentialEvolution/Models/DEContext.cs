using DotNetDifferentialEvolution.Interfaces;

namespace DotNetDifferentialEvolution.Models;

public class DEContext
{
    public int PopulationSize { get; init; }

    public int GenomeSize { get; init; }

    public int WorkersCount { get; init; }

    public ReadOnlyMemory<double> GenesLowerBound { get; init; }

    public ReadOnlyMemory<double> GenesUpperBound { get; init; }

    public IFitnessFunctionEvaluator FitnessFunctionEvaluator { get; init; }
}
