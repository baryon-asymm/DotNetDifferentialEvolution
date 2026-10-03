
namespace DotNetDifferentialEvolution.ControlParameterProviders;

/// <summary>
/// Supplies fixed control parameters for every individual. This reproduces the
/// behavior of classic differential evolution with constant F and CR.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ConstantControlParameterProvider"/> class.
/// </remarks>
/// <param name="mutationForce">The constant mutation factor (F).</param>
/// <param name="crossoverProbability">The constant crossover probability (CR).</param>
public class ConstantControlParameterProvider(
    double mutationForce,
    double crossoverProbability) : IControlParameterProvider
{
    private readonly double _mutationForce = mutationForce;
    private readonly double _crossoverProbability = crossoverProbability;

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
