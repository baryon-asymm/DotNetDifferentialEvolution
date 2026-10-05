namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>Where a trial's F and CR come from, each the CPU package's class of the same name (docs/ALGORITHMS.md, §§3.4–7).</summary>
internal enum ParameterRule
{
    /// <summary>One F and one CR for every trial; the CPU package's <c>ConstantControlParameterProvider</c>.</summary>
    Fixed = 0,

    /// <summary>jDE: F and CR per individual, each regenerated with probability 0.1 and inherited on survival; <c>JdeStrategy</c>.</summary>
    Jde = 1,

    /// <summary>JADE: CR from a normal distribution around μCR, F from a Cauchy around μF; <c>JadeStrategy</c>.</summary>
    Jade = 2,

    /// <summary>SHADE and L-SHADE: as JADE, around a memory slot drawn per trial; <c>ShadeStrategy</c>, <c>LShadeStrategy</c>.</summary>
    Shade = 3,
}
