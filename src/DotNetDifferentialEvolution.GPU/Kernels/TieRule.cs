namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// Whether a trial as good as its parent replaces it: the CPU package's <c>SelectionStrategy</c> with
/// <c>acceptsTies</c> true or false. An enum, not a <see cref="bool"/>, because ILGPU passes only blittable kernel
/// parameters.
/// </summary>
internal enum TieRule
{
    /// <summary>An equal trial replaces its parent: the fixed schemes, jDE, SHADE, L-SHADE.</summary>
    Accepted = 0,

    /// <summary>An equal trial does not replace its parent: JADE.</summary>
    Refused = 1,
}
