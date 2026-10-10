using ILGPU;
using ILGPU.Runtime;

namespace DotNetDifferentialEvolution.GPU.Objectives;

/// <summary>
/// The point results of one individual as <see cref="IGpuPointwiseFitnessFunction{TPoint}.Combine"/> sees them: a
/// read-only window onto device memory. Nothing can be written through it, so a combination cannot touch another
/// individual's results.
/// </summary>
/// <remarks>
/// Reading an index outside <c>0 ≤ index &lt; <see cref="Length"/></c> is undefined: kernels cannot throw, so there is
/// no bounds check.
/// </remarks>
/// <typeparam name="TPoint">The result of one point.</typeparam>
public readonly struct PointView<TPoint> : IEquatable<PointView<TPoint>>
    where TPoint : unmanaged
{
    private readonly ArrayView<TPoint> _points;

    /// <summary>Initializes a view onto <paramref name="points"/>.</summary>
    /// <param name="points">One individual's point results.</param>
    internal PointView(ArrayView<TPoint> points)
    {
        _points = points;
    }

    /// <summary>Gets the number of points, <c>P</c>.</summary>
    public int Length => _points.IntLength;

    /// <summary>Gets the result of the point at <paramref name="index"/>.</summary>
    /// <param name="index">The point, <c>0 ≤ index &lt; Length</c>.</param>
    public TPoint this[int index] => _points[index];

    /// <summary>Whether two views are the same window onto the same memory.</summary>
    /// <param name="left">The first view.</param>
    /// <param name="right">The second view.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    public static bool operator ==(PointView<TPoint> left, PointView<TPoint> right) => left.Equals(right);

    /// <summary>Whether two views are different windows.</summary>
    /// <param name="left">The first view.</param>
    /// <param name="right">The second view.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(PointView<TPoint> left, PointView<TPoint> right) => !left.Equals(right);

    /// <summary>Whether <paramref name="other"/> is the same window onto the same memory.</summary>
    /// <param name="other">The other view.</param>
    /// <returns><see langword="true"/> when the buffer, the start and the length are the same.</returns>
    public bool Equals(PointView<TPoint> other) =>
        ReferenceEquals(BufferOf(_points), BufferOf(other._points))
        && IndexOf(_points) == IndexOf(other._points)
        && _points.Length == other._points.Length;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PointView<TPoint> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(BufferOf(_points), IndexOf(_points), _points.Length);

    // ArrayView<T> implements both members explicitly; a constrained generic call reads them
    // without boxing the view.
    private static MemoryBuffer BufferOf<TView>(TView view)
        where TView : struct, IArrayView => view.Buffer;

    private static long IndexOf<TView>(TView view)
        where TView : struct, IContiguousArrayView => view.Index;
}
