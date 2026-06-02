
namespace DotNetDifferentialEvolution.ControlParameterProviders;

/// <summary>
/// Supplies a fresh mutation factor (F) sampled uniformly from <c>[minMutationForce, maxMutationForce]</c>
/// for every individual ("dither"), with a fixed crossover probability. Dithering F is a
/// cheap, well-regarded way to improve robustness over a single fixed F.
/// </summary>
public class DitheredControlParameterProvider : IControlParameterProvider
{
    private readonly double _minMutationForce;
    private readonly double _mutationForceRange;
    private readonly double _crossoverProbability;

    /// <summary>
    /// Initializes a new instance of the <see cref="DitheredControlParameterProvider"/> class.
    /// </summary>
    /// <param name="minMutationForce">The lower bound of the sampled mutation factor (F).</param>
    /// <param name="maxMutationForce">The upper bound of the sampled mutation factor (F).</param>
    /// <param name="crossoverProbability">The constant crossover probability (CR).</param>
    public DitheredControlParameterProvider(
        double minMutationForce,
        double maxMutationForce,
        double crossoverProbability)
    {
        if (minMutationForce > maxMutationForce)
            throw new ArgumentException("Minimum mutation force must be less than or equal to the maximum.");

        _minMutationForce = minMutationForce;
        _mutationForceRange = maxMutationForce - minMutationForce;
        _crossoverProbability = crossoverProbability;
    }

    /// <inheritdoc />
    public void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability)
    {
        ArgumentNullException.ThrowIfNull(randomProvider);

        mutationForce = _minMutationForce + randomProvider.NextDouble() * _mutationForceRange;
        crossoverProbability = _crossoverProbability;
    }
}
