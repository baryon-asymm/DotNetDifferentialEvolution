namespace DotNetDifferentialEvolution.GPU;

/// <summary>The last stage of the builder: the optional settings, then <see cref="Build"/>.</summary>
/// <typeparam name="TFunction">The objective.</typeparam>
public interface IGpuDifferentialEvolutionBuilder<TFunction>
    where TFunction : struct
{
    /// <summary>Makes the run reproducible: the same seed, device and package and ILGPU versions give a bit-identical result.</summary>
    /// <param name="seed">The seed, the Philox key.</param>
    /// <returns>This stage.</returns>
    IGpuDifferentialEvolutionBuilder<TFunction> WithSeed(int seed);

    /// <summary>Observes the population every <paramref name="everyNGenerations"/> generations, on the run's thread.</summary>
    /// <param name="handler">The observer.</param>
    /// <param name="everyNGenerations">The period; at least 1.</param>
    /// <returns>This stage.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="everyNGenerations"/> is below 1.</exception>
    IGpuDifferentialEvolutionBuilder<TFunction> WithPopulationUpdateHandler(IGpuPopulationUpdatedHandler handler, int everyNGenerations = 1);

    /// <summary>
    /// Samples the initial population in a box narrower than the search box: gene <c>j</c> of every individual is
    /// <c>lower + u·(upper − lower)</c> of the initial interval, from the draws that would give the search box's. Only the
    /// sampling reads it: repair, every scheme, the population reduction, the stagnation rule, the snapshots and the result keep
    /// the search box. An initial box equal to the search box gives the run it would have without the call, bit for bit. The
    /// values are copied; a second call replaces the first. Not to be combined with <see cref="WithInitialPopulation"/>.
    /// </summary>
    /// <param name="initialLowerBound">The lower bound of each gene's initial interval, <c>D</c> values.</param>
    /// <param name="initialUpperBound">The upper bound of each gene's initial interval, <c>D</c> values.</param>
    /// <returns>This stage.</returns>
    /// <exception cref="ArgumentException">
    /// A length other than <c>D</c>, a value that is not finite, a lower bound above its upper bound, or an interval not inside
    /// the search box (a value equal to its edge is inside); the message names the gene.
    /// </exception>
    IGpuDifferentialEvolutionBuilder<TFunction> WithInitialBounds(
        ReadOnlyMemory<double> initialLowerBound, ReadOnlyMemory<double> initialUpperBound);

    /// <summary>
    /// Starts the run from the given individuals instead of a sampled population: generation 0 is these genes as they are,
    /// evaluated by <see cref="Build"/> (N evaluations), and no random draw is spent on them. The values are copied; a second
    /// call replaces the first. Not to be combined with <see cref="WithInitialBounds"/>.
    /// </summary>
    /// <param name="genes">
    /// <c>N·D</c> values, individual-major (the genes of individual 0, then those of individual 1, and so on), N being the
    /// population size (under L-SHADE the initial one), each inside the search box.
    /// </param>
    /// <returns>This stage.</returns>
    /// <exception cref="ArgumentException">
    /// A length other than <c>N·D</c>, a value that is not finite or one outside the search box (a value equal to its edge is
    /// inside); the message names the individual and the gene.
    /// </exception>
    IGpuDifferentialEvolutionBuilder<TFunction> WithInitialPopulation(ReadOnlyMemory<double> genes);

    /// <summary>
    /// Opens the device, compiles the kernels, samples the initial population on the device (or takes the given genes) and
    /// evaluates it: costs N evaluations and the compile time.
    /// </summary>
    /// <returns>The optimizer, ready to run.</returns>
    /// <exception cref="InvalidOperationException">
    /// An explicit device cannot be opened; the message names it. Or both <see cref="WithInitialBounds"/> and
    /// <see cref="WithInitialPopulation"/> were called. Or a stage retained from before changed the bounds, the genome or the
    /// population size so that the initial box or the given genes no longer fit; the message names what does not.
    /// </exception>
    GpuDifferentialEvolution Build();
}
