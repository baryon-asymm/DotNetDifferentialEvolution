namespace DotNetDifferentialEvolution.Interfaces;

public interface IPopulationSamplingMaker
{
    public void SamplePopulation(
        Span<double> population);
}
