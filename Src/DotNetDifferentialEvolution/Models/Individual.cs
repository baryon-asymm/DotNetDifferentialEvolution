namespace DotNetDifferentialEvolution.Models;

public class Individual
{
    private readonly Memory<double> _genes;

    public Individual(
        IEnumerable<double> genes)
    {
        FitnessFunctionValue = double.MaxValue;
        _genes = genes.ToArray();
    }

    public Individual(
        double fitnessFunctionValue,
        IEnumerable<double> genes)
    {
        FitnessFunctionValue = fitnessFunctionValue;
        _genes = genes.ToArray();
    }

    public double FitnessFunctionValue { get; private set; }
    public ReadOnlyMemory<double> Genes => _genes;
}
