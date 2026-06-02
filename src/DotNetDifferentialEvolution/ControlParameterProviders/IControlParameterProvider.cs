
namespace DotNetDifferentialEvolution.ControlParameterProviders;

/// <summary>
/// Supplies the per-individual control parameters (mutation factor F and crossover
/// probability CR) used to build each trial vector.
/// </summary>
/// <remarks>
/// Classic DE uses constant parameters; self-adaptive variants (jDE, JADE, SHADE,
/// L-SHADE) sample fresh parameters per individual and adapt them between generations
/// via an <see cref="GenerationStrategies.IGenerationStrategy"/>.
/// </remarks>
public interface IControlParameterProvider
{
    /// <summary>
    /// Gets the control parameters to use for the specified individual in the current generation.
    /// </summary>
    /// <param name="individualIndex">The index of the individual to be mutated.</param>
    /// <param name="randomProvider">The random provider to use for sampling.</param>
    /// <param name="mutationForce">The mutation factor (F) to use.</param>
    /// <param name="crossoverProbability">The crossover probability (CR) to use.</param>
    public void GetControlParameters(
        int individualIndex,
        BaseRandomProvider randomProvider,
        out double mutationForce,
        out double crossoverProbability);
}
