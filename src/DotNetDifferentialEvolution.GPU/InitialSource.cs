namespace DotNetDifferentialEvolution.GPU;

/// <summary>Where generation 0 of a run comes from, and so which initialisation kernels <c>Build</c> compiles.</summary>
internal enum InitialSource
{
    /// <summary>Sampled on the device, in the search box or in the caller's initial box, then evaluated.</summary>
    Sampled = 0,

    /// <summary>The caller's genes, uploaded to the current population, then evaluated; nothing is sampled.</summary>
    Given = 1,
}
