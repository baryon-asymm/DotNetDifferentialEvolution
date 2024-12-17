namespace DotNetDifferentialEvolution.Models;

public class Population
{
    public int Generation { get; private set; }
    public ReadOnlyMemory<Individual> Individuals { get; init; }

    public int GenomeSize { get; init; }
    public int PopulationSize { get; init; }

    public int BestIndividualIndex { get; private set; }
    public Individual BestIndividual => this[BestIndividualIndex];

    public Individual this[
        int individualIndex] =>
        Individuals.Span[individualIndex];
}
