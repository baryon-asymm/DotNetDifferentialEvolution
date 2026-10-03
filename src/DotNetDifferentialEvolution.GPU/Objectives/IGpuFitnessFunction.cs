namespace DotNetDifferentialEvolution.GPU.Objectives;

/// <summary>
/// The objective, compiled into the kernel: implement it on a struct. <see cref="Evaluate"/> is
/// called once per individual per launch, one GPU thread each.
/// </summary>
/// <remarks>
/// <para>
/// Any data the objective needs (fit points, constants) it carries as fields: value types and
/// ILGPU <c>ArrayView</c>s allocated on the same accelerator the optimizer runs on.
/// </para>
/// <para>
/// The body is kernel code: value types only, no virtual calls, no allocation, no exceptions, no
/// strings. ILGPU reports a violation when the optimizer is built, not when the code is
/// compiled by C#.
/// </para>
/// </remarks>
public interface IGpuFitnessFunction
{
    /// <summary>The fitness of one individual; lower is better, and <see cref="double.NaN"/> ranks worst.</summary>
    /// <param name="genes">That individual's genes, read-only.</param>
    /// <returns>The fitness value.</returns>
    double Evaluate(GeneView genes);
}
