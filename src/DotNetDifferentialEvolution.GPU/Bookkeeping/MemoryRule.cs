namespace DotNetDifferentialEvolution.GPU.Bookkeeping;

/// <summary>
/// Which update a SHADE memory slot gets: SHADE's (<c>ShadeStrategy</c>), or L-SHADE's with the terminal CR and the
/// weighted Lehmer CR mean (<c>LShadeStrategy</c>). An enum, not a <see cref="bool"/>, because ILGPU passes only
/// blittable kernel parameters.
/// </summary>
internal enum MemoryRule
{
    /// <summary>SHADE: the weighted arithmetic CR mean, no terminal value.</summary>
    Shade = 0,

    /// <summary>L-SHADE: the weighted Lehmer CR mean and the terminal CR.</summary>
    LShade = 1,
}
