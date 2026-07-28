namespace DotNetDifferentialEvolution.Models;

/// <summary>
/// A population: its gene arena, its fitness values, how many individuals are live, and how long
/// an individual is — carried together rather than as four members whose relationship has to be
/// remembered at every use site.
/// </summary>
/// <param name="Genes">The flattened genes, allocated for <see cref="Capacity"/> individuals.</param>
/// <param name="FfValues">The fitness function value of each individual.</param>
/// <param name="Count">The number of live individuals; indices <c>0 .. Count - 1</c>.</param>
/// <param name="GenomeSize">The number of genes per individual.</param>
/// <remarks>
/// The buffers are allocated once and never resized, so <see cref="Count"/> and
/// <see cref="Capacity"/> diverge as soon as a strategy reduces the population — L-SHADE's Linear
/// Population Size Reduction does exactly that. Keeping the authoritative length next to the
/// arena it describes is what removes the question "which length applies here?", which is the
/// question that produced TD-3.
/// </remarks>
public readonly record struct PopulationView(
    Memory<double> Genes,
    Memory<double> FfValues,
    int Count,
    int GenomeSize)
{
    /// <summary>Gets the number of individuals the buffers were allocated for.</summary>
    public int Capacity => FfValues.Length;

    /// <summary>Gets the genes of one individual.</summary>
    /// <param name="individualIndex">The index of the individual.</param>
    /// <returns>The individual's genes.</returns>
    public Span<double> GenesOf(
        int individualIndex)
        => Genes.Span.Slice(individualIndex * GenomeSize, GenomeSize);

    /// <summary>Gets the genes of the live individuals, excluding any allocated tail.</summary>
    public Span<double> ActiveGenes => Genes.Span.Slice(0, Count * GenomeSize);

    /// <summary>Gets the fitness values of the live individuals, excluding any allocated tail.</summary>
    public Span<double> ActiveFfValues => FfValues.Span.Slice(0, Count);
}
