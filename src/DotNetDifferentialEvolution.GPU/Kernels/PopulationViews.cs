using ILGPU;

namespace DotNetDifferentialEvolution.GPU.Kernels;

/// <summary>
/// The device memory one launch works on. Populations are individual-major: individual <c>i</c>
/// holds genes <c>[i·D, (i+1)·D)</c>.
/// </summary>
/// <param name="Current">The current population, <c>N·D</c> genes; read by every thread.</param>
/// <param name="CurrentFitness">The current fitness values, <c>N</c>.</param>
/// <param name="Next">The next population, <c>N·D</c>; thread <c>i</c> writes slot <c>i</c> only.</param>
/// <param name="NextFitness">The next fitness values, <c>N</c>; thread <c>i</c> writes entry <c>i</c> only.</param>
/// <param name="Trial">The trial vectors, <c>N·D</c>; thread <c>i</c> writes slot <c>i</c> only.</param>
/// <param name="LowerBound">The lower bound of each gene, <c>D</c>.</param>
/// <param name="UpperBound">The upper bound of each gene, <c>D</c>.</param>
internal readonly record struct PopulationViews(
    ArrayView<double> Current,
    ArrayView<double> CurrentFitness,
    ArrayView<double> Next,
    ArrayView<double> NextFitness,
    ArrayView<double> Trial,
    ArrayView<double> LowerBound,
    ArrayView<double> UpperBound)
{
    /// <summary>The same views with the current and next populations exchanged: the swap at the end of a generation.</summary>
    /// <returns>The swapped views.</returns>
    public PopulationViews Swapped() => this with
    {
        Current = Next,
        CurrentFitness = NextFitness,
        Next = Current,
        NextFitness = CurrentFitness,
    };
}
