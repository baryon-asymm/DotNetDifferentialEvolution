
namespace DotNetDifferentialEvolution.ControlParameterProviders;

/// <summary>
/// Supplies fixed control parameters for every individual. This reproduces the
/// behavior of classic differential evolution with constant F and CR.
/// </summary>
public class ConstantControlParameterProvider : IControlParameterProvider
{
    private readonly double _mutationForce;
    private readonly double _crossoverProbability;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstantControlParameterProvider"/> class.
    /// </summary>
    /// <param name="mutationForce">The constant mutation factor (F).</param>
    /// <param name="crossoverProbability">The constant crossover probability (CR).</param>
    public ConstantControlParameterProvider(
        double mutationForce,
        double crossoverProbability)
    {
        _mutationForce = mutationForce;
        _crossoverProbability = crossoverProbability;
    }

    /// <inheritdoc />
    public void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability)
    {
        mutationForce = _mutationForce;
        crossoverProbability = _crossoverProbability;
    }
}
