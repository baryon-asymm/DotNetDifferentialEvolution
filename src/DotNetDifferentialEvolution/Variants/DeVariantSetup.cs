using DotNetDifferentialEvolution.ControlParameterProviders;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.MutationStrategies.Interfaces;
using DotNetDifferentialEvolution.SelectionStrategies.Interfaces;

namespace DotNetDifferentialEvolution.Variants;

/// <summary>
/// The bundle of decisions a DE variant installs. These belong together — jDE's self-adaptation
/// is meaningless without the mutation strategy that reads the parameters it adapts, and SHADE's
/// archive capacity is meaningless without the p-best strategy that draws from it — which is why
/// a variant hands the builder all of them at once instead of the caller assembling them.
/// </summary>
public readonly record struct DeVariantSetup
{
    /// <summary>The mutation operator. Its
    /// <see cref="IMutationStrategy.Requirements"/> must be satisfiable by the rest of this setup.
    /// </summary>
    public required IMutationStrategy MutationStrategy { get; init; }

    /// <summary>
    /// Where the mutation operator's per-individual F and CR come from. May be
    /// <see langword="null"/> only for a mutation strategy that declares no
    /// <see cref="MutationRequirements.ControlParameters"/>; the builder rejects the
    /// combination otherwise.
    /// </summary>
    public IControlParameterProvider? ControlParameterProvider { get; init; }

    /// <summary>
    /// The end-of-generation hook, typically the same object as
    /// <see cref="ControlParameterProvider"/> for an adaptive variant, since adapting the
    /// parameters and supplying them are two views of one piece of state.
    /// </summary>
    public IGenerationStrategy? GenerationStrategy { get; init; }

    /// <summary>
    /// How trials replace parents. When <see langword="null"/> the builder installs the greedy
    /// <see cref="SelectionStrategies.SelectionStrategy"/>, which is what every published variant
    /// in this library uses.
    /// </summary>
    public ISelectionStrategy? SelectionStrategy { get; init; }

    /// <summary>
    /// The external archive capacity in individuals; <c>0</c> disables the archive. Sized against
    /// <see cref="DeVariantConfiguration.PopulationSize"/> by the variant.
    /// </summary>
    public int ArchiveCapacity { get; init; }
}
