using DotNetDifferentialEvolution.GPU.Random;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The parameter rule of a generation kernel, a type argument of it: ILGPU compiles only the rule a run uses, so a kernel
/// of the fixed schemes or of jDE reaches no <c>Log</c>, <c>Cos</c> or <c>Tan</c>, and a caller's CUDA accelerator
/// without libdevice can run it (ACCEPTANCE.md, 7b). Implemented by empty structs that call
/// <see cref="ControlParameters"/>.
/// </summary>
internal interface IControlParameterRule
{
    /// <summary>F and CR of individual <paramref name="individual"/>.</summary>
    /// <typeparam name="TDraws">The draw source.</typeparam>
    /// <param name="draws">The individual's draws.</param>
    /// <param name="individual">The index i.</param>
    /// <param name="parameters">The fixed F and CR, the memory size.</param>
    /// <param name="strategy">jDE's F and CR per individual; JADE's means; SHADE's memory.</param>
    /// <param name="mutationForce">F.</param>
    /// <param name="crossoverProbability">CR; <see cref="double.NaN"/> for the fixed rule, whose threshold is precomputed.</param>
    void Draw<TDraws>(
        ref TDraws draws,
        int individual,
        StepParameters parameters,
        StrategyViews strategy,
        out double mutationForce,
        out double crossoverProbability)
        where TDraws : struct, IDrawSource;
}

/// <summary><see cref="ParameterRule.Fixed"/>: the run's F, no draw.</summary>
internal readonly struct FixedRule : IControlParameterRule
{
    /// <inheritdoc />
    public void Draw<TDraws>(ref TDraws draws, int individual, StepParameters parameters, StrategyViews strategy, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource
    {
        mutationForce = parameters.MutationForce;
        crossoverProbability = double.NaN;
    }
}

/// <summary><see cref="ParameterRule.Jde"/>: <see cref="ControlParameters.Jde"/> on the individual's F and CR.</summary>
internal readonly struct JdeRule : IControlParameterRule
{
    /// <inheritdoc />
    public void Draw<TDraws>(ref TDraws draws, int individual, StepParameters parameters, StrategyViews strategy, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource =>
        ControlParameters.Jde(
            ref draws, strategy.MutationForces[individual], strategy.CrossoverProbabilities[individual], out mutationForce, out crossoverProbability);
}

/// <summary><see cref="ParameterRule.Jade"/>: <see cref="ControlParameters.Jade"/> around μCR and μF.</summary>
internal readonly struct JadeRule : IControlParameterRule
{
    /// <inheritdoc />
    public void Draw<TDraws>(ref TDraws draws, int individual, StepParameters parameters, StrategyViews strategy, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource =>
        ControlParameters.Jade(ref draws, strategy.Adaptation[0], strategy.Adaptation[1], out mutationForce, out crossoverProbability);
}

/// <summary><see cref="ParameterRule.Shade"/>: <see cref="ControlParameters.Shade"/> around a memory slot.</summary>
internal readonly struct ShadeRule : IControlParameterRule
{
    /// <inheritdoc />
    public void Draw<TDraws>(ref TDraws draws, int individual, StepParameters parameters, StrategyViews strategy, out double mutationForce, out double crossoverProbability)
        where TDraws : struct, IDrawSource =>
        ControlParameters.Shade(ref draws, strategy.Adaptation, parameters.MemorySize, out mutationForce, out crossoverProbability);
}
