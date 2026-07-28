namespace DotNetDifferentialEvolution.RandomProviders;

/// <summary>
/// The randomness the trial-construction helpers draw on, expressed so that it can be supplied
/// as a <see langword="struct"/> type argument.
/// </summary>
/// <remarks>
/// <para>
/// A generic method instantiated over a <see langword="struct"/> gets its own JIT-compiled body,
/// so these calls are resolved statically and inlined rather than dispatched. That is the whole
/// point of the interface: the hot path draws once per gene, and a virtual call there is a
/// measurable share of the cost of building a trial.
/// </para>
/// <para>
/// It also keeps the helpers testable. <see cref="SeededRandomSource"/> is what the engine uses;
/// <see cref="ProviderRandomSource"/> accepts any <see cref="BaseRandomProvider"/>, including the
/// scripted fakes that make crossover and index selection exactly assertable in unit tests. Both
/// go through the same helper body.
/// </para>
/// </remarks>
internal interface IRandomSource
{
    /// <summary>Returns a non-negative random integer less than <paramref name="maxValue"/>.</summary>
    int Next(int maxValue);

    /// <summary>Returns the raw 64-bit output of the underlying generator.</summary>
    ulong NextULong();
}
