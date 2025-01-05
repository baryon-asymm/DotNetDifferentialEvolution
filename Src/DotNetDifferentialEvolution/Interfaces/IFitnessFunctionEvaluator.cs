namespace DotNetDifferentialEvolution.Interfaces;

public interface IFitnessFunctionEvaluator
{
    public double Evaluate(
        ReadOnlySpan<double> genes);
}
