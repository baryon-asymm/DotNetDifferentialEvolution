using DotNetDifferentialEvolution.GPU.Objectives;

namespace DotNetDifferentialEvolution.GPU;

/// <summary>
/// The third stage of the builder: the scheme, or a whole variant. The methods, their parameters and their defaults are
/// the CPU builder's (<c>IMutationStrategyRequired</c>), and so are their semantics (<c>docs/ALGORITHMS.md</c>, §§3–7).
/// A variant brings its own selection, as in the CPU package.
/// </summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuMutationStrategyRequired<TFunction>
    where TFunction : struct, IGpuFitnessFunction
{
    /// <summary>Uses DE/rand/1/bin, named as in the CPU builder. Needs N ≥ 4.</summary>
    /// <param name="mutationForce">F; finite and greater than 0.</param>
    /// <param name="crossoverProbability">CR; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">F is not finite or not positive, or CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithDefaultMutationStrategy(double mutationForce, double crossoverProbability);

    /// <summary>Uses DE/best/1/bin: <c>x_best + F·(x_r1 − x_r2)</c>. Needs N ≥ 3.</summary>
    /// <param name="mutationForce">F; finite and greater than 0.</param>
    /// <param name="crossoverProbability">CR; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">F is not finite or not positive, or CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithBestMutationStrategy(double mutationForce, double crossoverProbability);

    /// <summary>Uses DE/current-to-best/1/bin: <c>x_i + F·(x_best − x_i) + F·(x_r1 − x_r2)</c>. Needs N ≥ 3.</summary>
    /// <param name="mutationForce">F; finite and greater than 0.</param>
    /// <param name="crossoverProbability">CR; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">F is not finite or not positive, or CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithCurrentToBestMutationStrategy(double mutationForce, double crossoverProbability);

    /// <summary>Uses DE/rand/2/bin: <c>x_r1 + F·(x_r2 − x_r3) + F·(x_r4 − x_r5)</c>. Needs N ≥ 6.</summary>
    /// <param name="mutationForce">F; finite and greater than 0.</param>
    /// <param name="crossoverProbability">CR; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">F is not finite or not positive, or CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithRandTwoMutationStrategy(double mutationForce, double crossoverProbability);

    /// <summary>Uses DE/best/2/bin: <c>x_best + F·(x_r1 − x_r2) + F·(x_r3 − x_r4)</c>. Needs N ≥ 5.</summary>
    /// <param name="mutationForce">F; finite and greater than 0.</param>
    /// <param name="crossoverProbability">CR; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">F is not finite or not positive, or CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithBestTwoMutationStrategy(double mutationForce, double crossoverProbability);

    /// <summary>
    /// Uses jDE (Brest et al., 2006): DE/rand/1/bin with F and CR per individual, each regenerated with probability
    /// 0.1 (F uniform in [0.1, 1), CR in [0, 1)) and inherited when the trial replaces its parent. Needs N ≥ 4.
    /// </summary>
    /// <param name="initialMutationForce">Every individual's F at the start; finite and greater than 0.</param>
    /// <param name="initialCrossoverProbability">Every individual's CR at the start; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The initial F is not finite or not positive, or the initial CR is outside [0, 1].</exception>
    IGpuTerminationConditionRequired<TFunction> WithJde(double initialMutationForce = 0.5, double initialCrossoverProbability = 0.9);

    /// <summary>
    /// Uses JADE (Zhang and Sanderson, 2009): current-to-pbest/1/bin with an archive of beaten parents, CR from N(μCR,
    /// 0.1), F from Cauchy(μF, 0.1), the means adapted towards the improving trials; ties do not replace a parent.
    /// Needs N ≥ 4.
    /// </summary>
    /// <param name="pBestRate">p: the best round(p·N), at least 2, are the p-best pool; in (0, 1].</param>
    /// <param name="archiveSizeRate">The archive's capacity per individual; 0 for none.</param>
    /// <param name="adaptationRate">c, the weight of a generation's successes in the means; in [0, 1].</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its range or not finite.</exception>
    IGpuTerminationConditionRequired<TFunction> WithJade(double pBestRate = 0.1, double archiveSizeRate = 1.0, double adaptationRate = 0.1);

    /// <summary>
    /// Uses SHADE (Tanabe and Fukunaga, 2013): as JADE, with a success-history memory of H slots weighted by the
    /// improvement, p drawn per trial from [min(2/N, p), p], and ties accepted. Needs N ≥ 4.
    /// </summary>
    /// <param name="pBestRate">The largest p; in (0, 1].</param>
    /// <param name="archiveSizeRate">The archive's capacity per individual; 0 for none.</param>
    /// <param name="memorySize">H, at least 1.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its range or not finite.</exception>
    IGpuTerminationConditionRequired<TFunction> WithShade(double pBestRate = 0.2, double archiveSizeRate = 1.0, int memorySize = 100);

    /// <summary>
    /// Uses L-SHADE (Tanabe and Fukunaga, 2014): SHADE with the weighted Lehmer CR mean, a terminal CR, a fixed p, and
    /// the population reduced linearly from N to 4 as the evaluations reach <paramref name="maxEvaluationNumber"/>.
    /// An evaluation limit, if chosen, must be the same budget. Needs N ≥ 4.
    /// </summary>
    /// <param name="maxEvaluationNumber">The evaluation budget the reduction is planned over; at least 1.</param>
    /// <param name="pBestRate">p; in (0, 1].</param>
    /// <param name="archiveSizeRate">The archive's capacity per individual; 0 for none.</param>
    /// <param name="memorySize">H, at least 1.</param>
    /// <returns>The next stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A parameter is outside its range or not finite.</exception>
    IGpuTerminationConditionRequired<TFunction> WithLShade(
        long maxEvaluationNumber,
        double pBestRate = 0.11,
        double archiveSizeRate = 2.6,
        int memorySize = 6);
}
