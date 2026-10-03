using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Objectives;

/// <summary>
/// The genes of one individual as an objective sees them: a read-only window onto device memory.
/// Nothing can be written through it, which is what lets the kernel, not the objective, keep the
/// generation free of races (BOOT.md, invariant 2).
/// </summary>
/// <remarks>
/// Reading an index outside <c>0 ≤ index &lt; <see cref="Length"/></c> is undefined: kernels
/// cannot throw, so there is no bounds check.
/// </remarks>
public readonly struct GeneView : IEquatable<GeneView>
{
    private readonly ArrayView<double> _genes;

    /// <summary>Initializes a view onto <paramref name="genes"/>.</summary>
    /// <param name="genes">One individual's genes.</param>
    internal GeneView(ArrayView<double> genes)
    {
        _genes = genes;
    }

    /// <summary>Gets the number of genes, <c>D</c>.</summary>
    public int Length => _genes.IntLength;

    /// <summary>Gets the gene at <paramref name="index"/>.</summary>
    /// <param name="index">The index of the gene, <c>0 ≤ index &lt; Length</c>.</param>
    public double this[int index] => _genes[index];

    /// <summary>Whether two views are the same window onto the same memory.</summary>
    /// <param name="left">The first view.</param>
    /// <param name="right">The second view.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    public static bool operator ==(GeneView left, GeneView right) => left.Equals(right);

    /// <summary>Whether two views are different windows.</summary>
    /// <param name="left">The first view.</param>
    /// <param name="right">The second view.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(GeneView left, GeneView right) => !left.Equals(right);

    /// <summary>Whether <paramref name="other"/> is the same window onto the same memory.</summary>
    /// <param name="other">The other view.</param>
    /// <returns><see langword="true"/> when the buffer, the start and the length are the same.</returns>
    public bool Equals(GeneView other) =>
        ReferenceEquals(BufferOf(_genes), BufferOf(other._genes))
        && IndexOf(_genes) == IndexOf(other._genes)
        && _genes.Length == other._genes.Length;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GeneView other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(BufferOf(_genes), IndexOf(_genes), _genes.Length);

    // ArrayView<T> implements both members explicitly; a constrained generic call reads them
    // without boxing the view.
    private static MemoryBuffer BufferOf<TView>(TView view)
        where TView : struct, IArrayView => view.Buffer;

    private static long IndexOf<TView>(TView view)
        where TView : struct, IContiguousArrayView => view.Index;
}
