namespace DotNetDifferentialEvolution.GPU.Objectives;

/// <summary>
/// An objective made of <c>P</c> independent parts (experimental points, load cases, scenarios) and a combination of
/// their results, compiled into the kernel: implement it on a struct. The package evaluates the <c>N·P</c> parts in
/// <c>N·P</c> threads (<see cref="EvaluatePoint"/>) and combines them per individual (<see cref="Combine"/>).
/// </summary>
/// <remarks>
/// <para>
/// A pointwise objective whose <see cref="EvaluatePoint"/> and <see cref="Combine"/> perform the arithmetic of an
/// <see cref="IGpuFitnessFunction"/>, operation for operation and in the same order, gives the same run bit for bit on the
/// CPU accelerator. On a GPU the last bits may differ: the device compiler may fuse a multiply and an add of the monolithic
/// form into one operation, which the pointwise form, storing the product as a point result, cannot.
/// </para>
/// <para>
/// Data, body and visibility are those of <see cref="IGpuFitnessFunction"/>: value types and ILGPU <c>ArrayView</c>s as
/// fields, kernel code only, and the objective type and <typeparamref name="TPoint"/> visible to ILGPU's runtime assembly.
/// </para>
/// </remarks>
/// <typeparam name="TPoint">The result of one point: any unmanaged struct.</typeparam>
public interface IGpuPointwiseFitnessFunction<TPoint>
    where TPoint : unmanaged
{
    /// <summary>
    /// The result of one point of one individual. Called once per individual and point, each call in its own GPU thread,
    /// in no particular order.
    /// </summary>
    /// <param name="genes">That individual's genes, read-only.</param>
    /// <param name="point">The point, <c>0 ≤ point &lt; P</c>.</param>
    /// <returns>The point's result.</returns>
    TPoint EvaluatePoint(GeneView genes, int point);

    /// <summary>
    /// The fitness of one individual from its point results; lower is better, and <see cref="double.NaN"/> ranks worst.
    /// Called once per individual, in one thread, after all its points.
    /// </summary>
    /// <param name="genes">That individual's genes, read-only.</param>
    /// <param name="points">The results <see cref="EvaluatePoint"/> returned for that individual, in point order.</param>
    /// <returns>The fitness value.</returns>
    double Combine(GeneView genes, PointView<TPoint> points);
}
